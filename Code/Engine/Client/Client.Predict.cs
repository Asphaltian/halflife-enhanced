using System;

namespace GoldSrc;

partial class Client
{
	private const int MaxCommandMsec = 50;
	private const float TeleportDistance = 128f;
	private const float MaxPredictionError = 64f;
	private const float ShowErrorThreshold = 0.5f;
	private const float SmoothErrorThreshold = 0.25f;
	private const int PitchTraceCount = 6;

	[ConVar( "cl_smoothtime", ConVarFlags.Saved )]
	private static float SmoothTime { get; set; } = 0.1f;

	[ConVar( "cl_nosmooth" )]
	private static float NoSmooth { get; set; }

	[ConVar( "cl_showerror" )]
	private static float ShowError { get; set; }

	[ConVar( "cl_idealpitchscale", ConVarFlags.Saved )]
	private static float IdealPitchScale { get; set; } = 0.8f;

	private struct PredictedPlayer
	{
		public int MoveType;
		public int Solid;
		public int UseHull;
		public bool Active;
		public Vector3 Origin;
		public Vector3 Angles;
	}

	private readonly PredictedPlayer[] _predictedPlayers = new PredictedPlayer[Server.MaxClientsLimit];
	private readonly LocalState _splitState = new();
	private readonly float[] _pitchTrace = new float[PitchTraceCount];

	private Vector3 _simOrg;
	private Vector3 _simVel;
	private Vector3 _simAngles;
	private Vector3 _punchAngle;
	private Vector3 _viewHeight;
	private Vector3 _lastPredictedOrigin;
	private float _idealPitch;
	private int _onGround = -1;
	private int _predictedGround;
	private bool _moving;
	private int _waterLevel;
	private int _useHull;
	private int _viewModelIndex;
	private double _correctionTime;
	private int _showErrorCount;
	private int _lastUpdateSequence = -1;

	public int WaterLevel => _waterLevel;

	public void SetUpPlayerPrediction( bool includeLocalClient )
	{
		var frame = GetFrame( _parseCountMod );

		for ( var i = 0; i < _predictedPlayers.Length; i++ )
		{
			ref readonly var state = ref frame.PlayerState[i];
			ref var player = ref _predictedPlayers[i];

			player.Active = false;

			if ( state.MessageNum != _parseCount )
				continue;

			if ( state.ModelIndex == 0 )
				continue;

			if ( (state.Effects & (int)Effects.NoDraw) != 0 && !includeLocalClient && i == _playerNum )
				continue;

			player.MoveType = state.MoveType;
			player.Solid = state.Solid;
			player.UseHull = state.UseHull;
			player.Active = true;

			if ( i == _playerNum )
			{
				player.Origin = state.Origin;
				player.Angles = state.Angles;
			}
			else if ( GetEntity( i + 1 ) is { } entity )
			{
				player.Origin = entity.Origin;
				player.Angles = entity.Angles;
			}
		}
	}

	private void CheckPredictionError()
	{
		var origin = GetFrame( _parseCountMod ).PlayerState[_playerNum].Origin;
		var delta = origin - _predictedOrigins[_netchan.IncomingAcknowledged & UpdateMask];
		var length = MathLib.Length( delta );

		if ( length > MaxPredictionError )
			return;

		if ( ShowError != 0f && length > ShowErrorThreshold )
			NPrintf( (++_showErrorCount & 3) + 10, $"pred. error {length:F3} units" );

		_predictedOrigins[_parseCountMod] = origin;

		if ( length > SmoothErrorThreshold && MaxClients != 1 )
			_correctionTime = SmoothTime;
	}

	private void RedoPrediction()
	{
		if ( _netchan.IncomingSequence == _lastUpdateSequence )
			return;

		PredictMove( true );
		CheckPredictionError();
	}

	private void ClientPlayerMoveSound( SoundChannel channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		if ( !_pmove.RunFuncs )
			return;

		if ( _demoRecording )
			WriteDemoSound( (int)channel, sample, volume, attenuation, flags, pitch );

		PlaySound( _pmove.PlayerIndex + 1, _pmove.Origin, channel, sample, volume, attenuation, flags, pitch );
	}

	private void RunUsercmd( LocalState from, LocalState to, UserCmd cmd, bool runFuncs, ref double time, int randomSeed )
	{
		if ( cmd.Msec > MaxCommandMsec )
		{
			var split = cmd;

			split.Msec = (byte)(int)(cmd.Msec * 0.5);

			RunUsercmd( from, _splitState, split, runFuncs, ref time, randomSeed );

			split.Impulse = 0;
			split.WeaponSelect = 0;

			RunUsercmd( _splitState, to, split, runFuncs, ref time, randomSeed );
			return;
		}

		var pmove = _pmove;

		pmove.Time = (float)(time * 1000.0);
		pmove.Server = false;
		pmove.Multiplayer = MaxClients > 1;
		pmove.RunFuncs = runFuncs;
		pmove.FrameTime = (float)(cmd.Msec / 1000.0);

		to.CopyFrom( from );

		pmove.PlayerIndex = from.PlayerState.Number - 1;
		pmove.Flags = (EntityFlags)from.Client.Flags;
		pmove.InDuck = from.Client.InDuck != 0;
		pmove.TimeStepSound = from.Client.TimeStepSound;
		pmove.DuckTime = from.Client.DuckTime;
		pmove.SwimTime = from.Client.SwimTime;
		pmove.WaterJumpTime = from.Client.WaterJumpTime;
		pmove.WaterLevel = from.Client.WaterLevel;
		pmove.WaterType = from.Client.WaterType;
		pmove.OnGround = from.Client.Flags & (int)EntityFlags.OnGround;
		pmove.DeadFlag = from.Client.DeadFlag;
		pmove.Velocity = from.Client.Velocity;
		pmove.ViewOffset = from.Client.ViewOffset;
		pmove.Origin = from.PlayerState.Origin;
		pmove.BaseVelocity = from.PlayerState.BaseVelocity;
		pmove.PunchAngle = from.Client.PunchAngle;
		pmove.Angles = from.PlayerState.Angles;
		pmove.OldAngles = from.PlayerState.Angles;
		pmove.Friction = from.PlayerState.Friction;
		pmove.UseHull = from.PlayerState.UseHull;
		pmove.OldButtons = from.PlayerState.OldButtons;
		pmove.Dead = _health < 1;
		pmove.Spectator = 0;
		pmove.MoveType = (MoveType)from.PlayerState.MoveType;
		pmove.Gravity = from.PlayerState.Gravity;
		pmove.Effects = from.PlayerState.Effects;
		pmove.StepLeft = from.PlayerState.StepLeft;
		pmove.FallVelocity = from.PlayerState.FallVelocity;
		pmove.PhysInfo = _physInfo;
		pmove.MaxSpeed = _moveVars.MaxSpeed;
		pmove.ClientMaxSpeed = from.Client.MaxSpeed;
		pmove.Cmd = cmd;

		pmove.User1 = from.Client.IUser1;
		pmove.User2 = from.Client.IUser2;
		pmove.User3 = from.Client.IUser3;
		pmove.User4 = from.Client.IUser4;
		pmove.FloatUser1 = from.Client.FUser1;
		pmove.FloatUser2 = from.Client.FUser2;
		pmove.FloatUser3 = from.Client.FUser3;
		pmove.FloatUser4 = from.Client.FUser4;
		pmove.VectorUser1 = from.Client.VUser1;
		pmove.VectorUser2 = from.Client.VUser2;
		pmove.VectorUser3 = from.Client.VUser3;
		pmove.VectorUser4 = from.Client.VUser4;

		_clientDll.PlayerMove( pmove, false );

		_predictedGround = pmove.OnGround;

		if ( _predictedGround > 0 )
			_predictedGround = _predictedGround < pmove.PhysEnts.Count ? pmove.PhysEnts[_predictedGround].Info : 0;

		to.Client.Flags = (int)pmove.Flags;
		to.Client.InDuck = pmove.InDuck ? 1 : 0;
		to.Client.TimeStepSound = pmove.TimeStepSound;
		to.Client.DuckTime = (int)pmove.DuckTime;
		to.Client.SwimTime = (int)pmove.SwimTime;
		to.Client.WaterJumpTime = (int)pmove.WaterJumpTime;
		to.Client.WaterLevel = pmove.WaterLevel;
		to.Client.WaterType = pmove.WaterType;
		to.Client.MaxSpeed = pmove.ClientMaxSpeed;
		to.Client.DeadFlag = pmove.DeadFlag;
		to.Client.Velocity = pmove.Velocity;
		to.Client.ViewOffset = pmove.ViewOffset;
		to.PlayerState.Origin = pmove.Origin;
		to.PlayerState.BaseVelocity = pmove.BaseVelocity;
		to.Client.PunchAngle = pmove.PunchAngle;
		to.PlayerState.OldButtons = pmove.Cmd.Buttons;
		to.PlayerState.Friction = pmove.Friction;
		to.PlayerState.MoveType = (int)pmove.MoveType;
		to.PlayerState.Effects = pmove.Effects;
		to.PlayerState.UseHull = pmove.UseHull;
		to.PlayerState.StepLeft = pmove.StepLeft;
		to.PlayerState.FallVelocity = pmove.FallVelocity;

		to.Client.IUser1 = pmove.User1;
		to.Client.IUser2 = pmove.User2;
		to.Client.IUser3 = pmove.User3;
		to.Client.IUser4 = pmove.User4;
		to.Client.FUser1 = pmove.FloatUser1;
		to.Client.FUser2 = pmove.FloatUser2;
		to.Client.FUser3 = pmove.FloatUser3;
		to.Client.FUser4 = pmove.FloatUser4;
		to.Client.VUser1 = pmove.VectorUser1;
		to.Client.VUser2 = pmove.VectorUser2;
		to.Client.VUser3 = pmove.VectorUser3;
		to.Client.VUser4 = pmove.VectorUser4;

		_clientDll.PostRunCmd( from, to, ref cmd, runFuncs, time, (uint)randomSeed );

		time += cmd.Msec / 1000.0;
	}

	private void PredictMove( bool repredicting )
	{
		if ( _state != ClientState.Active )
			return;

		SetUpPlayerPrediction( false );

		var realTime = (float)Host.RealTime;

		if ( _intermission != 0 )
			return;

		if ( _validSequence == 0 )
			return;

		if ( _netchan.OutgoingSequence - _netchan.IncomingAcknowledged + 1 >= UpdateMask )
			return;

		_simAngles = ViewAngles;

		if ( _demoPlayback )
			return;

		var frame = GetFrame( _parseCountMod );
		var from = GetPredictedFrame( _parseCountMod );

		from.PlayerState = frame.PlayerState[_playerNum];
		from.Client = LocalClientData.Read( frame.ClientData );

		for ( var i = 0; i < from.WeaponData.Length; i++ )
			from.WeaponData[i] = WeaponData.Read( frame.WeaponData[i] );

		if ( frame.Invalid )
			return;

		var time = frame.Time;

		_onGround = -1;

		var fromCommand = _netchan.IncomingAcknowledged & UpdateMask;
		var toCommand = -1;
		LocalState to = null;

		PushPMStates();
		SetSolidPlayers( _playerNum );

		var count = 1;

		while ( _netchan.IncomingAcknowledged + count < _netchan.OutgoingSequence + (repredicting ? 0 : 1) )
		{
			var sequence = _netchan.IncomingAcknowledged + count;
			var index = sequence & UpdateMask;

			to = GetPredictedFrame( _parseCountMod + count );
			toCommand = index;

			RunUsercmd( from, to, _commands[index].Cmd, !repredicting && !_commands[index].ProcessedFuncs, ref time, sequence );

			_commands[index].ProcessedFuncs = true;
			_predictedOrigins[index] = to.PlayerState.Origin;

			if ( realTime <= _commands[index].SentTime )
				break;

			count++;
			from = to;
			fromCommand = index;

			if ( count >= UpdateBackup - 1 )
				break;
		}

		PopPMStates();

		if ( count >= UpdateMask )
			return;

		if ( to is null )
		{
			to = from;
			toCommand = fromCommand;

			if ( !repredicting )
				return;
		}

		var fromTime = _commands[fromCommand].SentTime;
		var toTime = _commands[toCommand].SentTime;
		var fraction = 0f;

		if ( toTime != fromTime )
			fraction = Math.Max( (realTime - fromTime) / (toTime - fromTime), 0f );

		var teleported = false;

		for ( var i = 0; i < 3; i++ )
		{
			if ( Math.Abs( (double)(to.PlayerState.Origin[i] - from.PlayerState.Origin[i]) ) > TeleportDistance )
			{
				teleported = true;
				break;
			}
		}

		if ( teleported )
		{
			_simVel = to.Client.Velocity;
			_simOrg = to.PlayerState.Origin;
			_punchAngle = to.Client.PunchAngle;
			_viewHeight = to.Client.ViewOffset;
		}
		else
		{
			_simOrg = from.PlayerState.Origin + ((to.PlayerState.Origin - from.PlayerState.Origin) * fraction);
			_simVel = from.Client.Velocity + ((to.Client.Velocity - from.Client.Velocity) * fraction);
			_punchAngle = from.Client.PunchAngle + ((to.Client.PunchAngle - from.Client.PunchAngle) * fraction);

			if ( from.PlayerState.UseHull == to.PlayerState.UseHull )
				_viewHeight = from.Client.ViewOffset + ((to.Client.ViewOffset - from.Client.ViewOffset) * fraction);
			else
				_viewHeight = to.Client.ViewOffset;
		}

		_onGround = (to.Client.Flags & (int)EntityFlags.OnGround) != 0 ? _predictedGround : -1;
		_waterLevel = to.Client.WaterLevel;
		_useHull = to.PlayerState.UseHull;
		_viewModelIndex = to.Client.ViewModel;
		_moving = false;

		if ( _onGround > 0 && _onGround < _maxEdicts && GetEntity( _onGround ) is { } ground )
		{
			var delta = ground.CurState.Origin - ground.PrevState.Origin;

			delta.z = 0f;

			if ( MathLib.Length( delta ) > 0f )
			{
				_moving = true;
				_correctionTime = 0.0;
			}
		}

		if ( !_moving && _correctionTime > 0.0 && NoSmooth == 0f && SmoothTime != 0f )
		{
			if ( !repredicting )
				_correctionTime -= Host.FrameTime;

			if ( SmoothTime <= 0f )
				Cvar.Set( "cl_smoothtime", "0.1" );

			if ( _correctionTime < 0.0 )
				_correctionTime = 0.0;

			if ( SmoothTime <= _correctionTime )
				_correctionTime = SmoothTime;

			var scale = (float)(1.0 - (float)(_correctionTime / SmoothTime));

			_simOrg = _lastPredictedOrigin + ((_simOrg - _lastPredictedOrigin) * scale);
		}

		_lastPredictedOrigin = _simOrg;

		SetIdealPitch();
	}

	private void SetIdealPitch()
	{
		if ( _onGround == -1 )
			return;

		var angle = (double)(float)(((_simAngles.y * Math.PI) + (_simAngles.y * Math.PI)) / 360.0);
		var sin = (float)Math.Sin( angle );
		var cos = (float)Math.Cos( angle );

		for ( var i = 0; i < PitchTraceCount; i++ )
		{
			var top = new Vector3( ((i + 3) * cos * 12f) + _simOrg.x, ((i + 3) * sin * 12f) + _simOrg.y, _viewHeight.z + _simOrg.z );
			var bottom = new Vector3( top.x, top.y, top.z - 160f );

			var trace = _pmove.PlayerTrace( top, bottom, TraceFlags.StudioBox, -1 );

			if ( trace.AllSolid )
				return;

			if ( trace.Fraction == 1f )
				return;

			_pitchTrace[i] = top.z + (trace.Fraction * (bottom.z - top.z));
		}

		var direction = 0;
		var steps = 0;

		for ( var j = 1; j < PitchTraceCount; j++ )
		{
			var step = (int)(_pitchTrace[j] - _pitchTrace[j - 1]);

			if ( step > -0.1 && step < 0.1 )
				continue;

			if ( direction != 0 && (step - direction > 0.1 || step - direction < -0.1) )
				return;

			steps++;
			direction = step;
		}

		if ( direction == 0 )
		{
			_idealPitch = 0f;
			return;
		}

		if ( steps < 2 )
			return;

		_idealPitch = -direction * IdealPitchScale;
	}
}
