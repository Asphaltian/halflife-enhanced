using System;

namespace GoldSrc;

public sealed partial class Client : GameObjectSystem<Client>
{
	private const double MaxFrameRate = 100.0;
	private const double CommandTime = 1.0 / (MaxFrameRate + 0.5);
	private const int MaxCommandsPerFrame = 10;
	private const double MaxTimeDemoFrameTime = 0.25;

	public const float DefaultSensitivity = 3f;

	[ConVar( "gl_lowlatency", ConVarFlags.Saved )]
	public static float LowLatency { get; set; }

	[ConVar( "m_rawinput", ConVarFlags.Saved )]
	public static float RawInput { get; set; }

	[ConVar( "gl_widescreen_yfov", ConVarFlags.Saved )]
	public static float WidescreenYFov { get; set; } = 1f;

	[ConVar( "viewsize", ConVarFlags.Saved )]
	public static float ViewSize { get; set; } = 120f;

	[ConVar( "cl_fixmodelinterpolationartifacts" )]
	private static float FixModelInterpolation { get; set; } = 1f;

	[ConVar( "cl_nodelta" )]
	private static float NoDelta { get; set; }

	public static float Sensitivity { get; set; } = DefaultSensitivity;

	public static bool StudioInterpolation { get; set; } = true;

	public float FieldOfView { get; set; } = 90f;

	public double ClientTime { get; private set; }

	public int MaxClients => _maxClients;

	public Vector3 ViewOrigin => _viewOrigin;

	internal bool Intermission => _intermission != 0;

	public Vector3 ViewAngles;

	private static Client _owner;

	private readonly bool _inert;

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

	public Client( Scene scene ) : base( scene )
	{
		_inert = scene.IsEditor || _owner is not null;

		if ( _owner is null )
		{
			Cvar.ReadArchive();
			Keys.ReadArchive();
			Server.SetGameDirectory();
		}

		if ( _inert )
			return;

		_owner = this;

		if ( Application.IsDedicatedServer )
			_state = ClientState.Dedicated;
		else
			CdAudio.Init();

		Listen( Stage.StartUpdate, 0, Frame, "Client" );

		Delta.InitRegistry();
	}

	public override void Dispose()
	{
		if ( _inert )
		{
			base.Dispose();
			return;
		}

		_owner = null;

		if ( _state != ClientState.Dedicated )
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
		ClearRagdolls();

		_dynamicLightTexture?.Dispose();
		_dynamicLightTexture = null;

		base.Dispose();
	}

	private void Frame()
	{
		if ( _inert )
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
			_pmove.PlaySound = ClientPlayerMoveSound;
			_pmove.TraceTexture = TraceTexture;
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

		if ( !RunTicks( server, out var ran ) )
			return;

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

		if ( ran && GetEntity( _viewEntity ) is { } viewEntity && (viewEntity.PacketEffects & Effects.MuzzleFlash) == Effects.MuzzleFlash )
			_viewModel.Effects |= Effects.MuzzleFlash;

		UpdateClientTime();

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

		EmitEntities( server, fraction );
		UpdateScreen( fraction, teleported );
	}

	private bool RunTicks( Server server, out bool ran )
	{
		_accumulator += RealTime.Delta;

		ran = false;

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

			return false;
		}

		if ( _accumulator >= CommandTime )
			_accumulator = 0.0;

		return true;
	}

	private void UpdateClientTime()
	{
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
	}

	private void EmitEntities( Server server, float fraction )
	{
		LinkPlayers();
		LinkPlayerLight( GetEntity( _playerNum + 1 ) );
		LinkPacketEntities( fraction );

		_clientDll.CreateEntities();
		TempEntUpdate();
		UpdateRagdolls();
		FireEvents();
		AnimateLight();
		DrawDecals();
		server.Interpolate( fraction );
	}

	private void UpdateScreen( float fraction, bool teleported )
	{
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
			EntityAngles = GetEntity( _playerNum + 1 )?.Angles ?? default,
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

		MathLib.AngleVectors( refParams.ViewAngles, out var forward, out var right, out var up );

		SetupViewCamera( camera, refParams, true );
		UpdateEnhancements( camera );
		BeginDecalCommands( camera );

		Scene.RenderAttributes.Set( "Fog", _waterLevel > 2 ? WaterFog : Vector4.Zero );
		Scene.RenderAttributes.Set( "ClientTime", (float)ClientTime );
		Scene.RenderAttributes.Set( "WaterSides", WaterSides );

		EmitWater( refParams.ViewOrigin, forward, right, up, FieldOfView, Screen.Width, Screen.Height );

		PushDlights();

		foreach ( var entity in _visibleEntities )
			entity.Draw();

		var light = LightVec( refParams.ViewModelOrigin, refParams.ViewModelOrigin - new Vector3( 0f, 0f, 2048f ) );

		_lightLevel = (byte)((Math.Min( light.Blue, 255 ) + Math.Min( light.Green, 255 ) + Math.Min( light.Red, 255 )) / 3);

		_viewModel.Origin = refParams.ViewModelOrigin;
		_viewModel.Angles = refParams.ViewModelAngles;
		_viewModel.Draw( this, _health );

		var tanHalfX = MathF.Tan( camera.FieldOfView * 0.008726646f );
		var tanHalfY = tanHalfX * Screen.Height / Screen.Width;

		DrawParticles( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
		DrawBeams( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
		DrawTriangles( 0, camera, refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );

		RenderNextViews( camera, ref refParams, paramOrigin, paramAngles );

		DrawTempEntities();

		HudRedraw( _intermission == IntermissionScoreboard, refParams.ViewAngles, tanHalfX, tanHalfY );
		VGuiPaint( !GameUiActive );

		DecayLights( (float)(ClientTime - _oldTime) );

		Sound.Paused = _paused;
		Sound.Update( refParams.ViewOrigin, forward, right, up );

		if ( GameUiActive )
			GameUiFrame( true );
		else
			_gameUi?.RunFrame( ScreenWidth, ScreenHeight, true, MaxClients > 1 );
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
		UpdateSoundFade();

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
}
