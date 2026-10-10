using System;

namespace GoldSrc;

public sealed partial class Client : GameObjectSystem<Client>
{
	private const double MaxFrameRate = 100.0;
	private const double CommandTime = 1.0 / (MaxFrameRate + 0.5);
	private const int MaxCommandsPerFrame = 10;
	private const double MaxTimeDemoFrameTime = 0.25;

	[ConVar( "gl_lowlatency", ConVarFlags.Saved )]
	public static float LowLatency { get; set; }

	[ConVar( "m_rawinput", ConVarFlags.Saved )]
	public static float RawInput { get; set; }

	public const float DefaultSensitivity = 3f;

	public static float Sensitivity { get; set; } = DefaultSensitivity;

	[ConVar( "gl_widescreen_yfov", ConVarFlags.Saved )]
	public static float WidescreenYFov { get; set; } = 1f;

	[ConVar( "cl_fixmodelinterpolationartifacts" )]
	private static float FixModelInterpolation { get; set; } = 1f;

	[ConVar( "cl_nodelta" )]
	private static float NoDelta { get; set; }

	public float FieldOfView { get; set; } = 90f;

	public Vector3 ViewAngles;

	private ClientDll _clientDll;
	private double _accumulator;
	private Vector3 _previousSimOrg;
	private bool _simOrgValid;
	private bool _previousSimOrgValid;
	private Vector3 _simOrigin;
	private Vector3 _previousViewHeight;
	private double _time;
	private double _clientTime;
	private double _oldClientTime;
	private ViewEntity _viewModel;
	private HudCanvas _hudCanvas;
	private Vector3 _crosshairAngle;
	private Vector3 _viewAngles;
	private Vector3 _viewModelOrigin;
	private Vector3 _viewModelAngles;
	private DLight _playerLight;
	private double _oldTime;
	private int _intermission;
	private byte _lightLevel;
	private Vector3 _viewForward;
	private Vector3 _viewRight;
	private Vector3 _viewUp;

	internal bool Intermission => _intermission != 0;
	private readonly bool _editor;

	public double ClientTime { get; private set; }

	public int MaxClients => _maxClients;

	public static bool StudioInterpolation { get; set; } = true;

	public Vector3 ViewOrigin => _viewOrigin;

	[ConVar( "viewsize", ConVarFlags.Saved )]
	public static float ViewSize { get; set; } = 120f;

	public Client( Scene scene ) : base( scene )
	{
		_editor = scene.IsEditor;

		if ( Application.IsDedicatedServer )
			_state = ClientState.Dedicated;

		Listen( Stage.StartUpdate, 0, Frame, "Client" );

		if ( _state != ClientState.Dedicated )
			CdAudio.Init();

		Cvar.ReadArchive();
		Keys.ReadArchive();
		Server.SetGameDirectory();

		if ( !_editor )
		{
			Delta.InitRegistry();
		}
	}

	public override void Dispose()
	{
		if ( !_editor && _state != ClientState.Dedicated )
		{
			CdAudio.Shutdown();
			ClientDll.Unload( this );
		}

		foreach ( var decalObject in _decalObjects.Values )
			decalObject.Delete();

		_decalObjects.Clear();

		if ( _decalCamera.IsValid() )
			_decalCamera.RemoveCommandList( _decalCommands );

		ClearLightVolume();
		ClearSky();

		_dynamicLightTexture?.Dispose();
		_dynamicLightTexture = null;

		base.Dispose();
	}

	private void Frame()
	{
		if ( _editor )
			return;

		if ( _state == ClientState.Dedicated )
		{
			DedicatedFrame();
			return;
		}

		if ( ++_frameCount != _loadingGrabFrame )
			LoadingFramePresented();

		Keys.Frame();
		CheckGamma();
		VoiceFrame( (float)Host.FrameTime );

		if ( !_drawLoading && Input.EscapePressed )
		{
			Input.EscapePressed = false;

			GameUiEscape( LevelName.Length != 0 );
		}

		Host.Frame();
		CdAudio.Frame();

		var server = Server.Get( Scene );

		Sound ??= new SoundEngine();

		if ( _clientDll is null )
		{
			TextMessageInit();
			VGuiStartup();

			_clientDll = ClientDll.Load( this );
			_clientDll.HookUserMsg( "ScreenShake", ScreenShakeMessage );
			_clientDll.HookUserMsg( "ScreenFade", ScreenFadeMessage );

			_pmove.LoadFile = Server.LoadMountText;
			_pmove.MoveVars = _moveVars;
			_clientDll.PlayerMoveInit( _pmove );
		}

		if ( _drawLoading && ((!server.Active && !Networking.IsClient) || InLevel) )
			return;

		Scene.TimeScale = InLevel && (server.Active ? server.TimeStopped : _paused) ? 0f : 1f;

		if ( !server.Active && _state == ClientState.Disconnected )
		{
			CheckForResend();

			if ( _state == ClientState.Disconnected )
			{
				MenuFrame();
				return;
			}
		}

		if ( _viewModel is null )
		{
			_viewModel = new GameObject( true, "viewmodel" ).AddComponent<ViewEntity>();
			_viewModel.GameObject.NetworkMode = NetworkMode.Never;
		}

		_hudCanvas ??= HudCanvas.Create( this );

		server.KeyDestGame = !MessageMode;

		GameUiActive = _gameUiActivated || !InLevel;

		if ( InLevel )
		{
			UpdateClientData();

			if ( !GameUiActive )
				_clientDll.MouseMove( ref ViewAngles );
		}

		_accumulator += RealTime.Delta;

		var ran = false;

		ClearStickyEffects();

		_weaponAnimReceived = false;

		_hostAbort = false;

		if ( _timeDemo )
		{
			ran = true;
			_accumulator = 0.0;

			RunTick( server, Math.Min( (double)RealTime.Delta, MaxTimeDemoFrameTime ) );
		}

		for ( var i = 0; !_timeDemo && !_hostAbort && _accumulator >= CommandTime && i < MaxCommandsPerFrame && !server.HasPendingChangeLevel; i++ )
		{
			ran = true;
			_accumulator -= CommandTime;

			RunTick( server, CommandTime );
		}

		if ( _hostAbort )
		{
			_accumulator = 0.0;

			return;
		}

		if ( _accumulator >= CommandTime )
			_accumulator = 0.0;

		if ( !InLevel )
		{
			if ( !_drawLoading && !(_holdLoadingFrame && _signon == SignOns) )
			{
				if ( _holdLoadingFrame )
					ReleaseLoadingFrame();

				MenuFrame();
			}

			return;
		}

		if ( _holdLoadingFrame )
			ReleaseLoadingFrame();

		GameUiActive = _gameUiActivated;

		var localEntity = GetEntity( _playerNum + 1 );

		if ( ran && GetEntity( _viewEntity ) is { } viewEntity && (viewEntity.PacketEffects & Effects.MuzzleFlash) == Effects.MuzzleFlash )
			_viewModel.Effects |= Effects.MuzzleFlash;

		_oldTime = ClientTime;

		if ( _messageTime == _oldMessageTime || _timeDemo )
		{
			ClientTime = _messageTime;

			if ( _demoPlayback )
				_oldTime = ClientTime - Time.Delta;
		}
		else
		{
			ClientTime = (_netchan.Loopback ? _messageTime : _clientTime) - CommandTime + _accumulator;
		}

		if ( _state == ClientState.Active && _validSequence != 0 && !GetFrame( _parseCountMod ).Invalid )
		{
			var lerp = 1f;

			if ( _messageTime != _oldMessageTime && !_timeDemo && ExInterp > 0.001 )
				lerp = (float)((ClientTime - _messageTime) / ExInterp);

			_commands[(_netchan.OutgoingSequence - 1) & UpdateMask].FrameLerp = lerp;
		}

		if ( ran )
		{
			_viewModel.ResentEveryCommand = _weaponAnimReceived && _weaponAnimReceivedLastCommand;
			_weaponAnimReceivedLastCommand = _weaponAnimReceived;
		}

		if ( server.HasPendingChangeLevel )
			return;

		var fraction = (float)(_accumulator / CommandTime);

		var teleported = !_previousSimOrgValid;

		for ( var i = 0; i < 3; i++ )
			teleported |= Math.Abs( (double)(_simOrg[i] - _previousSimOrg[i]) ) > TeleportDistance;

		_simOrigin = teleported ? _simOrg : Vector3.Lerp( _previousSimOrg, _simOrg, fraction );

		LinkPlayers();
		LinkPlayerLight( localEntity );
		LinkPacketEntities( fraction );

		_clientDll.CreateEntities();
		TempEntUpdate();
		FireEvents();
		AnimateLight();
		DrawDecals();
		server.Interpolate( fraction );

		_time += Time.Delta;

		if ( _state is not ClientState.Dedicated and not ClientState.Disconnected )
			_clientDll.CamThink();

		var refParams = new RefParams
		{
			FrameTime = Time.Delta,
			Paused = _paused,
			Intermission = _intermission != 0,
			NoClip = IsNoClipping,
			IdealPitch = _idealPitch,
			Smoothing = _pushMsec != 0,
			Time = (float)_time,
			OnGround = _onGround != -1,
			WaterLevel = _waterLevel,
			SimVelocity = _simVel,
			SimOrigin = _simOrigin,
			ViewHeight = teleported ? _viewHeight : Vector3.Lerp( _previousViewHeight, _viewHeight, fraction ),
			ClientViewAngles = ViewAngles,
			ViewOrigin = _viewOrigin,
			ViewAngles = _viewAngles,
			ViewModelOrigin = _viewModelOrigin,
			ViewModelAngles = _viewModelAngles,
			Health = _health,
			PunchAngle = _punchAngle,
			MaxClients = _maxClients,
			MoveVars = _moveVars,
			EntityAngles = localEntity?.Angles ?? default,
			PointContents = PointContents,
			WaterEntityScale = WaterEntityScale,
			CrosshairAngle = _crosshairAngle,
			ViewSize = ViewSize,
			Forward = _viewForward,
			Right = _viewRight,
			Up = _viewUp,
			Spectator = _spectator,
			ViewEntity = _viewEntity,
			PlayerNum = _playerNum,
			MaxEntities = _maxEdicts,
			DemoPlayback = _demoPlayback,
			Hardware = 1,
			Cmd = _cmd,
			ViewportWidth = ScreenWidth,
			ViewportHeight = ScreenHeight
		};

		_viewModel.NoModel = false;
		_viewModel.ModelIndex = _viewModelIndex;
		_viewModel.Index = _playerNum + 1;

		_clientDll.CalcRefdef( ref refParams );

		if ( !_demoPlayback )
			SetDemoViewInfo( refParams, refParams.ViewModelOrigin, _viewModelIndex );

		if ( _demoPlayback && !_spectator )
		{
			GetDemoViewInfo( ref refParams, ref refParams.ViewModelOrigin, ref _viewModelIndex );

			refParams.OnlyClientDraw = 0;
			refParams.ViewModelAngles = new Vector3( -refParams.ViewAngles.x, refParams.ViewAngles.y, refParams.ViewAngles.z );
		}

		ViewAngles = refParams.ClientViewAngles;

		var paramOrigin = refParams.ViewOrigin;
		var paramAngles = refParams.ViewAngles;

		if ( !refParams.Intermission && !refParams.Paused && ChaseActive != 0f )
			ChaseUpdate( ref refParams );

		ForceCvars( _maxClients > 1 );

		_viewForward = refParams.Forward;
		_viewRight = refParams.Right;
		_viewUp = refParams.Up;

		_viewOrigin = refParams.ViewOrigin;
		_viewAngles = refParams.ViewAngles;
		_viewModelOrigin = refParams.ViewModelOrigin;
		_viewModelAngles = refParams.ViewModelAngles;

		if ( Scene.Camera is not { } camera )
			return;

		MathLib.AngleVectors( refParams.ViewAngles, out var viewForward, out var viewRight, out var viewUp );

		SetupViewCamera( camera, refParams, true );
		UpdateEnhancements( camera );
		BeginDecalCommands( camera );

		Scene.RenderAttributes.Set( "Fog", _waterLevel > 2 ? WaterFog : Vector4.Zero );
		Scene.RenderAttributes.Set( "ClientTime", (float)ClientTime );
		Scene.RenderAttributes.Set( "WaterSides", WaterSides );

		EmitWater( refParams.ViewOrigin, viewForward, viewRight, viewUp, FieldOfView, Screen.Width, Screen.Height );

		PushDlights();

		var light = LightVec( refParams.ViewModelOrigin, refParams.ViewModelOrigin - new Vector3( 0f, 0f, 2048f ) );

		_lightLevel = (byte)((Math.Min( light.Blue, 255 ) + Math.Min( light.Green, 255 ) + Math.Min( light.Red, 255 )) / 3);

		_viewModel.Origin = refParams.ViewModelOrigin;
		_viewModel.Angles = refParams.ViewModelAngles;
		_viewModel.Draw( this, _health );

		MathLib.AngleVectors( refParams.ViewAngles, out var forward, out var right, out var up );

		var tanHalfX = MathF.Tan( camera.FieldOfView * 0.008726646f );

		DrawParticles( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfX * Screen.Height / Screen.Width );
		DrawBeams( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfX * Screen.Height / Screen.Width );
		DrawTriangles( 0, camera, refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfX * Screen.Height / Screen.Width );

		RenderNextViews( camera, ref refParams, paramOrigin, paramAngles );

		DrawTempEntities();

		HudRedraw( _intermission == 1, refParams.ViewAngles, tanHalfX, tanHalfX * Screen.Height / Screen.Width );
		VGuiPaint( !GameUiActive );

		DecayLights( (float)(ClientTime - _oldTime) );

		Sound.Paused = _paused;
		Sound.Update( refParams.ViewOrigin, forward, right, up );

		if ( GameUiActive )
			GameUiFrame( true );
		else
			_gameUi?.RunFrame( ScreenWidth, ScreenHeight, true, MaxClients > 1 );
	}

	private const string OnlyClientDrawTag = "onlyclientdraw";
	private const int ViewCount = 2;

	private int _drawView;
	private bool _nextViewWorld;

	public const string FirstViewTag = "view0";
	public const string NextViewTag = "view1";

	private static string ViewTag( int view ) => view == 0 ? FirstViewTag : NextViewTag;

	public CameraComponent NextViewCamera => _nextViewWorld ? _nextViewCamera : null;

	private CameraComponent _nextViewCamera;

	private void SetupViewCamera( CameraComponent camera, in RefParams refParams, bool first )
	{
		var width = Math.Max( refParams.ViewportWidth, 1 );
		var height = Math.Max( refParams.ViewportHeight, 1 );

		camera.WorldPosition = refParams.ViewOrigin;
		camera.WorldRotation = new Angles( refParams.ViewAngles.x, refParams.ViewAngles.y, refParams.ViewAngles.z );
		camera.FovAxis = CameraComponent.Axis.Horizontal;
		camera.FieldOfView = ScreenFieldOfView( FieldOfView, (float)width / height );
		camera.Viewport = new Vector4( (float)refParams.ViewportX / ScreenWidth, (float)refParams.ViewportY / ScreenHeight, (float)width / ScreenWidth, (float)height / ScreenHeight );

		var onlyClientDraw = refParams.OnlyClientDraw != 0;

		if ( onlyClientDraw != camera.RenderTags.Has( OnlyClientDrawTag ) )
		{
			if ( onlyClientDraw )
				camera.RenderTags.Add( OnlyClientDrawTag );
			else
				camera.RenderTags.Remove( OnlyClientDrawTag );
		}

		if ( first )
		{
			if ( onlyClientDraw != _firstViewOnlyClientDraw )
			{
				_firstViewOnlyClientDraw = onlyClientDraw;

				if ( onlyClientDraw )
				{
					_firstViewBackground = camera.BackgroundColor;
					camera.BackgroundColor = Color.Black;
				}
				else
				{
					camera.BackgroundColor = _firstViewBackground;
				}
			}
		}
		else
		{
			camera.ClearFlags = onlyClientDraw ? ClearFlags.Depth | ClearFlags.Stencil : ClearFlags.All;
			camera.BackgroundColor = Color.Black;
		}
	}

	private bool _firstViewOnlyClientDraw;
	private Color _firstViewBackground;

	private void RenderNextViews( CameraComponent camera, ref RefParams refParams, Vector3 paramOrigin, Vector3 paramAngles )
	{
		if ( !camera.RenderExcludeTags.Has( ViewTag( 1 ) ) )
			camera.RenderExcludeTags.Add( ViewTag( 1 ) );

		if ( refParams.NextView == 0 )
		{
			if ( _nextViewCamera.IsValid() && _nextViewCamera.Enabled )
			{
				_nextViewCamera.Enabled = false;
				_nextViewWorld = false;

				ClearTriangles( 1 );
				HideViewEffects( 1 );
			}

			return;
		}

		refParams.ViewOrigin = paramOrigin;
		refParams.ViewAngles = paramAngles;

		_clientDll.CalcRefdef( ref refParams );

		ViewAngles = refParams.ClientViewAngles;

		if ( !refParams.Intermission && !refParams.Paused && ChaseActive != 0f )
			ChaseUpdate( ref refParams );

		_viewForward = refParams.Forward;
		_viewRight = refParams.Right;
		_viewUp = refParams.Up;

		_viewOrigin = refParams.ViewOrigin;
		_viewAngles = refParams.ViewAngles;
		_viewModelOrigin = refParams.ViewModelOrigin;
		_viewModelAngles = refParams.ViewModelAngles;

		if ( !_nextViewCamera.IsValid() )
		{
			var gameObject = new GameObject( true, "next view" );

			gameObject.NetworkMode = NetworkMode.Never;

			_nextViewCamera = gameObject.AddComponent<CameraComponent>();
			_nextViewCamera.IsMainCamera = false;
			_nextViewCamera.RenderExcludeTags.Add( ViewTag( 0 ) );

			gameObject.AddComponent<GammaSpace>();
		}

		_nextViewCamera.Enabled = true;
		_nextViewCamera.Priority = camera.Priority + 1;
		_nextViewCamera.ZNear = camera.ZNear;
		_nextViewCamera.ZFar = camera.ZFar;

		SetupViewCamera( _nextViewCamera, refParams, false );

		MathLib.AngleVectors( refParams.ViewAngles, out var forward, out var right, out var up );

		var tanHalfX = MathF.Tan( _nextViewCamera.FieldOfView * 0.008726646f );

		var tanHalfY = tanHalfX * Math.Max( refParams.ViewportHeight, 1 ) / Math.Max( refParams.ViewportWidth, 1 );

		_nextViewWorld = refParams.OnlyClientDraw == 0;

		if ( _nextViewWorld )
		{
			SetFrustum( _nextViewFrustum, refParams.ViewOrigin, forward, right, up, FieldOfView, Math.Max( refParams.ViewportWidth, 1 ), Math.Max( refParams.ViewportHeight, 1 ) );
			MarkViewLeaves( refParams.ViewOrigin );

			_drawView = 1;

			DrawParticles( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
			DrawBeams( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );

			_drawView = 0;
		}
		else
		{
			HideViewEffects( 1 );
		}

		DrawTriangles( 1, _nextViewCamera, refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
	}

	private void MenuFrame()
	{
		GameUiActive = true;

		GameUiFrame( false );

		MathLib.AngleVectors( _viewAngles, out var forward, out var right, out var up );

		Sound.Paused = false;
		Sound.Update( _viewOrigin, forward, right, up );
	}

	private void DedicatedFrame()
	{
		if ( !Host.FilterTime( RealTime.Delta ) )
			return;

		Host.Frame();

		var server = Server.Get( Scene );

		server.ClientActive = true;
		server.Frame( Host.FrameTime );
	}

	private void RunTick( Server server, double frameTime )
	{
		Host.AdvanceTime( frameTime );

		CheckClientState();

		server.ClientActive = _state == ClientState.Active;

		_previousSimOrg = _simOrg;
		_previousViewHeight = _viewHeight;
		_previousSimOrgValid = _simOrgValid;

		server.StoreInterpolation();

		Move();

		server.Frame( frameTime );

		_lastUpdateSequence = _netchan.IncomingSequence;

		ReadPackets();

		if ( _hostAbort )
			return;

		_clientDll?.Frame( Host.FrameTime );

		RedoPrediction();

		_simOrgValid = _state == ClientState.Active;

		CheckForResend();
		AdjustClock();

		_gameUiActiveLastCommand = _gameUiActivated;

		if ( GameGauge != 0f )
			GameGaugeFrame( (float)RealTime.Now );

		Host.FrameCount++;
	}

	private void ReadPackets()
	{
		_oldClientTime = _clientTime;
		_clientTime += Host.FrameTime;

		if ( _demoPlayback )
			ReadDemoPackets();

		while ( !_demoPlayback && _netchan.Incoming.TryDequeue( out var packet ) )
		{
			if ( _state is ClientState.Disconnected or ClientState.Connecting )
				continue;

			if ( _netchan.Process( packet, _netMessage ) )
				ParseServerMessage( _netMessage );
		}

		if ( _state is not ClientState.Dedicated and not ClientState.Disconnected && _netchan.IncomingReady )
		{
			if ( _netchan.CopyNormalFragments() is { } fragments )
			{
				_netMessage.BeginReading( fragments, fragments.Length );

				ParseServerMessage( _netMessage );
			}

			if ( _netchan.CopyFileFragments() )
				ProcessFile( true, _netchan.IncomingFileName );
		}

		SetSolidEntities();

		if ( _state >= ClientState.Connected && !_demoPlayback && Host.RealTime - _netchan.LastReceived > Timeout )
		{
			ExplainDisconnection( true, "#GameUI_ServerConnectionTimeout" );
			Disconnect();

			return;
		}

		NetCheckTimeouts();
	}

	private void LinkPlayerLight( ClientEntity player )
	{
		var effects = player?.PacketEffects ?? default;

		var newFlashlight = NewFlashlight != 0f && (effects & Effects.BrightLight) == 0 && (effects & Effects.DimLight) == Effects.DimLight;

		if ( !newFlashlight )
			FlashlightOff();

		if ( newFlashlight || (effects & (Effects.BrightLight | Effects.DimLight)) == 0 )
		{
			if ( _playerLight is { Key: 1 } )
				_playerLight.Die = (float)ClientTime;

			_playerLight = null;

			if ( newFlashlight )
				UpdateFlashlight( player.Origin + _viewHeight );

			return;
		}

		var light = _playerLight is { Key: 1 } ? _playerLight : AllocDlight( 1 );

		if ( (effects & Effects.BrightLight) == Effects.BrightLight )
		{
			light.R = 250;
			light.G = 250;
			light.B = 250;
			light.Radius = 400f;
			light.Origin = player.Origin + new Vector3( 0f, 0f, 16f );
		}
		else
		{
			MathLib.AngleVectors( _viewAngles, out var forward, out _, out _ );

			var start = player.Origin + _viewHeight;
			var end = start + (forward * 2000f);

			_pmove.UseHull = 2;

			var trace = _pmove.PlayerTrace( start, end, TraceFlags.StudioBox, -1 );

			light.Origin = trace.Ent > 0 && _pmove.PhysEnts[trace.Ent].StudioModel is not null ? _pmove.PhysEnts[trace.Ent].Origin : trace.EndPos;

			var falloff = trace.Fraction * 2000f;

			falloff = falloff >= 500f ? 500f / falloff : 1f;

			var brightness = (byte)(int)(falloff * falloff * 255f);

			light.Radius = 80f;
			light.R = brightness;
			light.G = brightness;
			light.B = brightness;
		}

		_playerLight = light;
		light.Die = (float)(ClientTime + 0.2);
	}

	private const int MaxQueuedEvents = 64;

	private const int EventReliable = 1 << 1;

	private readonly List<(double FireTime, QueuedEvent Event)> _events = [];
	private readonly byte[] _eventImage = new byte[EventArgs.Size];
	private readonly byte[] _nullEventImage = new byte[EventArgs.Size];

	private bool _weaponAnimReceived;
	private bool _weaponAnimReceivedLastCommand;

	public ViewEntity ViewModel => _viewModel;

	public bool IsLocal( int playerNumber ) => playerNumber == _playerNum;

	public void WeaponAnimation( int sequence, int body )
	{
		_viewModel.WeaponAnim( sequence, body );
		_weaponAnimReceived = true;

		if ( _demoRecording )
			WriteDemoWeaponAnim( sequence, body );
	}

	public SoundEngine Sound { get; private set; }

	public void PlaySound( int entityIndex, Vector3 origin, SoundChannel channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		var name = (flags & SoundSentenceFlag) != 0 ? $"!{Sound.SentenceName( Common.Atoi( sample[1..] ) )}" : sample;
		var sfx = name[0] == '!' ? new Sfx { Name = name } : Sound.PrecacheSound( name );

		if ( channel == SoundChannel.Static )
			Sound.StartStaticSound( entityIndex, (int)channel, sfx, origin, volume, attenuation, flags, pitch );
		else
			Sound.StartDynamicSound( entityIndex, (int)channel, sfx, origin, volume, attenuation, flags, pitch );
	}

	public void PlaybackEvent( EventFlags flags, int eventIndex, float delay, Vector3 origin, Vector3 angles, float fparam1, float fparam2, int iparam1, int iparam2, int bparam1, int bparam2 )
	{
		if ( (flags & EventFlags.Server) == EventFlags.Server )
			return;

		var args = new EventArgs
		{
			Origin = origin,
			Angles = angles,
			FParam1 = fparam1,
			FParam2 = fparam2,
			IParam1 = iparam1,
			IParam2 = iparam2,
			BParam1 = bparam1,
			BParam2 = bparam2,
			EntIndex = _playerNum + 1,
			Velocity = _simVel,
			Ducking = _useHull == 1 ? 1 : 0
		};

		QueueEvent( (int)flags, eventIndex, delay, args );

		if ( _demoRecording )
			WriteDemoEvent( (int)flags, eventIndex, delay, args );
	}

	public void PlaySoundByName( string name, float volume )
	{
		volume = Math.Clamp( volume, 0f, 1f );

		if ( Sound.PrecacheSound( name ) is not { } sfx )
		{
			Log.Trace( $"invalid sound {name}" );
			return;
		}

		Sound.StartDynamicSound( _viewEntity, (int)SoundChannel.Item, sfx, _viewOrigin, volume, 1f, 0, 100 );
	}

	public void StopSound( int entityIndex, SoundChannel channel, string sample )
	{
		PlaySound( entityIndex, Vector3.Zero, channel, sample, 0f, 0f, Server.SoundStop, Server.PitchNormal );
	}

	private const int SoundSentenceFlag = 1 << 4;
	public ClientEntity GetEntityByIndex( int index ) => GetEntity( index );

	public string GetPlayerName( int index )
	{
		if ( index < 1 || index > _maxClients )
			return null;

		return GetPlayerInfo( index - 1 ).Name is { Length: > 0 } name ? name : null;
	}

	private void QueueEvent( int flags, int index, float delay, in EventArgs args )
	{
		var fireTime = delay != 0f ? (float)(delay + ClientTime) : 0f;
		var queued = new QueuedEvent( index, index >= 0 && index < MaxEvents ? _eventPrecache[index] : null, args, flags );

		if ( _events.Count < MaxQueuedEvents )
		{
			_events.Add( (fireTime, queued) );
			return;
		}

		if ( (flags & EventReliable) == 0 )
			return;

		for ( var i = 0; i < _events.Count; i++ )
		{
			if ( (_events[i].Event.Flags & EventReliable) != 0 )
				continue;

			_events[i] = (fireTime, queued);
			return;
		}
	}

	private void ParseEvent( MessageReader message )
	{
		var delta = Delta.LookupRegistration( "event_t" );
		var pack = CurrentFrame.Entities;
		var args = new EventArgs();
		var delay = 0f;

		message.StartBitReading();

		var count = (int)message.ReadBits( 5 );

		for ( var i = 0; i < count; i++ )
		{
			var index = (int)message.ReadBits( 10 );
			var packetIndex = -1;

			if ( message.ReadBits( 1 ) != 0 )
			{
				packetIndex = (int)message.ReadBits( 11 );

				if ( message.ReadBits( 1 ) != 0 )
				{
					args.Write( _eventImage );

					delta.ParseDelta( message, _nullEventImage, _eventImage, _messageTime );

					args = EventArgs.Read( _eventImage );
				}
			}

			if ( message.ReadBits( 1 ) != 0 )
				delay = (float)((float)message.ReadBits( 16 ) / 100.0);

			if ( packetIndex == -1 )
				continue;

			if ( packetIndex < pack.Count )
			{
				ref readonly var state = ref pack.Entities[packetIndex];

				args.EntIndex = state.Number;

				if ( MathLib.VectorCompare( args.Origin, Vector3.Zero ) )
					args.Origin = state.Origin;

				if ( MathLib.VectorCompare( args.Angles, Vector3.Zero ) )
					args.Angles = state.Angles;

				args.Angles = Common.NormalizeAngles( args.Angles );

				if ( IsPlayerIndex( state.Number ) )
				{
					args.Angles.x = (float)(args.Angles.x * -3.0);
					args.Velocity = Vector3.Zero;

					if ( state.Number - 1 == _playerNum )
					{
						args.Velocity = _simVel;
					}
					else if ( GetEntity( state.Number ) is { } entity )
					{
						var time = entity.CurState.AnimTime - entity.PrevState.AnimTime;

						args.Velocity = time != 0f ? (entity.CurState.Velocity - entity.PrevState.Velocity) * (1f / time) : entity.CurState.Velocity;
					}

					args.Ducking = state.UseHull == 1 ? 1 : 0;
				}
			}
			else if ( args.EntIndex == 0 )
			{
				Log.Trace( "CL_ParseEvent:  Received non-packet entity index 0 for event\n" );
			}
			else if ( IsPlayerIndex( args.EntIndex ) )
			{
				args.Angles.x = (float)(args.Angles.x * -3.0);
			}

			QueueEvent( 0, index, delay, args );
		}

		message.EndBitReading();
	}

	private void ParseEventReliable( MessageReader message )
	{
		Array.Clear( _eventImage );

		message.StartBitReading();

		var index = (int)message.ReadBits( 10 );

		Delta.LookupRegistration( "event_t" ).ParseDelta( message, _nullEventImage, _eventImage, _messageTime );

		var args = EventArgs.Read( _eventImage );
		var delay = message.ReadBits( 1 ) != 0 ? (float)((float)message.ReadBits( 16 ) / 100.0) : 0f;

		if ( IsPlayerIndex( args.EntIndex ) )
			args.Angles.x = (float)(args.Angles.x * -3.0);

		QueueEvent( EventReliable, index, delay, args );

		message.EndBitReading();
	}

	public void KillEvents( int entityIndex, string name )
	{
		for ( var i = 0; i < _events.Count; i++ )
		{
			var (fireTime, queued) = _events[i];

			if ( queued.Name == name && queued.Args.EntIndex == entityIndex )
				_events[i] = (fireTime, default);
		}
	}

	private void FireEvents()
	{
		for ( var i = 0; i < _events.Count; i++ )
		{
			var (fireTime, queued) = _events[i];

			if ( queued.Name is not null && fireTime != 0.0 && fireTime > ClientTime )
				continue;

			_events.RemoveAt( i-- );

			if ( queued.Name is not null )
				_clientDll.FireEvent( queued );
		}
	}

	private static float ScreenFieldOfView( float fieldOfView, float aspect )
	{
		if ( WidescreenYFov != 0f && aspect > 1.5f )
		{
			var half = (float)(Math.Tan( fieldOfView * 0.008726646f ) * (aspect * 0.75f));
			var degrees = (float)(Math.Atan( half ) * 57.29577951308232);

			fieldOfView = degrees + degrees;
		}

		if ( fieldOfView < 1f || fieldOfView > 179f )
			fieldOfView = 90f;

		return fieldOfView;
	}
}
