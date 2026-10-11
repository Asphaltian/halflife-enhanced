namespace GoldSrc;

partial class Server
{
	private const int MaxCommandMsec = 50;

	public Edict Player => GetEdict( 1 );

	private readonly PlayerMove _pmove = new();

	public ServerClient ConnectLocalClient() => ConnectClient( Client.UserInfoString(), true );

	public void SetPhysicsKeyValue( Edict client, string key, string value )
	{
		if ( client is null || client.Index < 1 || client.Index > MaxClients )
		{
			Con.Printf( "tried to PF_SetPhysicsKeyValue a non-client\n" );
			return;
		}

		Info.SetValueForKey( ref _clients[client.Index - 1].PhysInfo, key, value, MaxInfoString );
	}

	public bool CanSkipPlayer( Edict client )
	{
		if ( client is null || client.Index < 1 || client.Index > MaxClients )
		{
			Con.Printf( "tried to PF_CanSkipPlayer a non-client\n" );
			return false;
		}

		return _clients[client.Index - 1].LocalWeapons;
	}

	public string GetPhysicsInfoString( Edict client )
	{
		if ( client is null || client.Index < 1 || client.Index > MaxClients )
		{
			Con.Printf( "tried to PF_GetPhysicsInfoString a non-client\n" );
			return "";
		}

		return _clients[client.Index - 1].PhysInfo;
	}

	private void RunCmd( UserCmd cmd, int randomSeed )
	{
		var client = _hostClient;
		var player = GetEdict( client.EdictIndex );

		if ( client.IgnoreCmdTime > Host.RealTime )
		{
			client.CmdTime += cmd.Msec / 1000.0;
			return;
		}

		client.IgnoreCmdTime = 0.0;

		if ( cmd.Msec > MaxCommandMsec )
		{
			var msec = cmd.Msec;

			cmd.Msec = (byte)(int)(msec / 2.0);
			RunCmd( cmd, randomSeed );

			cmd.Msec = (byte)(int)(msec / 2.0);
			cmd.Impulse = 0;
			RunCmd( cmd, randomSeed );

			return;
		}

		if ( !client.FakeClient )
			SetupMove( client );

		GameDll.CmdStart( player, cmd, randomSeed );

		var frameTime = (float)(cmd.Msec * 0.001);

		client.SvTimeBase += frameTime;
		client.CmdTime += cmd.Msec / 1000.0;

		if ( cmd.Impulse != 0 )
		{
			player.Impulse = cmd.Impulse;

			if ( cmd.Impulse == FullUpdateImpulse )
				ForceFullClientsUpdate( client );
		}

		player.ClBaseVelocity = Vector3.Zero;
		player.Button = cmd.Buttons;

		_touchedPhysObject = false;

		InitPlayerShadow( player );
		CheckSceneGround( player );
		CheckMovingGround( player, frameTime );

		_pmove.OldAngles = player.ViewAngle;

		if ( player.FixAngle == 0 )
			player.ViewAngle = cmd.ViewAngles;

		Globals.Time = (float)client.SvTimeBase;
		player.PlayerPreThink();

		PlayerRunThink( player, frameTime, client.SvTimeBase );

		if ( MathLib.Length( player.BaseVelocity ) > 0f )
			player.ClBaseVelocity = player.BaseVelocity;

		SetupPlayerMove( client, player, cmd );
		AddLinksToPlayerMove( player, player.Origin );

		_pmove.FrameTime = frameTime;
		_pmove.RunFuncs = true;

		GameDll.Move( _pmove, true );

		FinishMove( player );

		SetMinMaxSize( player, PlayerMove.PlayerMins[_pmove.UseHull], PlayerMove.PlayerMaxs[_pmove.UseHull] );

		if ( player.Solid != Solid.Not )
		{
			LinkEdict( player, true );

			var velocity = player.Velocity;

			foreach ( var touch in _pmove.TouchIndex )
			{
				var other = GetEdict( _pmove.PhysEnts[touch.Ent].Info );

				var trace = new Trace
				{
					AllSolid = touch.AllSolid,
					StartSolid = touch.StartSolid,
					InOpen = touch.InOpen,
					InWater = touch.InWater,
					Fraction = touch.Fraction,
					EndPos = touch.EndPos,
					PlaneNormal = touch.PlaneNormal,
					PlaneDist = touch.PlaneDist,
					Ent = other,
					HitGroup = touch.HitGroup
				};

				player.Velocity = touch.DeltaVelocity;
				Impact( other, player, trace );
			}

			player.Velocity = velocity;
		}

		Globals.Time = (float)client.SvTimeBase;
		Globals.FrameTime = frameTime;

		player.PlayerPostThink();

		PostThinkShadow( player );

		_shadowArrivalTime += frameTime;

		UpdateShadowPosition( _newShadowPosition, _newShadowVelocity, _shadowArrivalTime );

		if ( !client.FakeClient )
			RestoreMove( client );
	}

	private static void CheckMovingGround( Edict player, float frameTime )
	{
		if ( (player.EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && player.GroundEntity is { } ground && (ground.EntityFlags & EntityFlags.Conveyor) == EntityFlags.Conveyor )
		{
			if ( (player.EntityFlags & EntityFlags.BaseVelocity) == EntityFlags.BaseVelocity )
				player.BaseVelocity += ground.Speed * ground.MoveDir;
			else
				player.BaseVelocity = ground.Speed * ground.MoveDir;

			player.EntityFlags |= EntityFlags.BaseVelocity;
		}

		if ( (player.EntityFlags & EntityFlags.BaseVelocity) != EntityFlags.BaseVelocity )
		{
			player.Velocity += (float)((frameTime * 0.5) + 1.0) * player.BaseVelocity;
			player.BaseVelocity = Vector3.Zero;
		}

		player.EntityFlags &= ~EntityFlags.BaseVelocity;
	}

	private bool PlayerRunThink( Edict player, float frameTime, double time )
	{
		if ( (player.EntityFlags & (EntityFlags.KillMe | EntityFlags.Dormant)) == 0 )
		{
			var thinkTime = player.NextThink;

			if ( thinkTime <= 0f || thinkTime > frameTime + time )
				return true;

			if ( thinkTime < time )
				thinkTime = (float)time;

			player.NextThink = 0f;
			Globals.Time = thinkTime;
			player.DispatchThink();
		}

		if ( (player.EntityFlags & EntityFlags.KillMe) == EntityFlags.KillMe )
			FreeEdict( player );

		return IsInUse( player );
	}

	private void PlayerMovePlaySound( SoundChannel channel, string sample, float volume, float attenuation, int flags, int pitch )
	{
		StartSound( true, GetEdict( _hostClient.EdictIndex ), (int)channel, sample, (int)(volume * 255f), attenuation, flags, pitch );
	}

	private string PlayerMoveTraceTexture( int ground, Vector3 start, Vector3 end )
	{
		if ( ground < 0 || ground >= _pmove.PhysEnts.Count || _pmove.PhysEnts[ground].Model is null )
			return null;

		var edict = GetEdict( _pmove.PhysEnts[ground].Info );
		return edict is null ? null : TraceTexture( edict, start, end );
	}

	private void SetupPlayerMove( ServerClient client, Edict player, UserCmd cmd )
	{
		_pmove.Server = true;
		_pmove.Multiplayer = MaxClients > 1;
		_pmove.Time = (float)(1000.0 * client.SvTimeBase);
		_pmove.PhysInfo = client.PhysInfo;
		_pmove.UseHull = (player.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? 1 : 0;
		_pmove.MaxSpeed = SvMaxSpeed;
		_pmove.ClientMaxSpeed = player.MaxSpeed;
		_pmove.DuckTime = player.DuckTime;
		_pmove.InDuck = player.InDuck;
		_pmove.TimeStepSound = player.TimeStepSound;
		_pmove.StepLeft = player.StepLeft;
		_pmove.FallVelocity = player.FallVelocity;
		_pmove.SwimTime = player.SwimTime;
		_pmove.OldButtons = player.OldButtons;

		_pmove.Velocity = player.Velocity;
		_pmove.MoveDir = player.MoveDir;
		_pmove.Angles = player.ViewAngle;
		_pmove.BaseVelocity = player.BaseVelocity;
		_pmove.ViewOffset = player.ViewOffset;
		_pmove.PunchAngle = player.PunchAngle;
		_pmove.DeadFlag = player.DeadFlag;
		_pmove.Effects = (int)player.Effects;
		_pmove.Gravity = player.Gravity;
		_pmove.Friction = player.Friction;
		_pmove.Spectator = 0;
		_pmove.WaterJumpTime = player.TeleportTime;
		_pmove.Cmd = cmd;
		_pmove.Dead = player.Health <= 0f;
		_pmove.MoveType = player.MoveType;
		_pmove.Flags = player.EntityFlags;
		_pmove.PlayerIndex = player.Index - 1;

		_pmove.User1 = player.IUser1;
		_pmove.User2 = player.IUser2;
		_pmove.User3 = player.IUser3;
		_pmove.User4 = player.IUser4;
		_pmove.FloatUser1 = player.FUser1;
		_pmove.FloatUser2 = player.FUser2;
		_pmove.FloatUser3 = player.FUser3;
		_pmove.FloatUser4 = player.FUser4;
		_pmove.VectorUser1 = player.VUser1;
		_pmove.VectorUser2 = player.VUser2;
		_pmove.VectorUser3 = player.VUser3;
		_pmove.VectorUser4 = player.VUser4;

		_pmove.Origin = player.Origin;
		_pmove.MoveVars = MoveVars;
	}

	private void FinishMove( Edict player )
	{
		player.DeadFlag = _pmove.DeadFlag;
		player.Effects = (Effects)_pmove.Effects;
		player.TeleportTime = _pmove.WaterJumpTime;
		player.WaterLevel = _pmove.WaterLevel;
		player.WaterType = _pmove.WaterType;
		player.EntityFlags = _pmove.Flags;
		player.Friction = _pmove.Friction;
		player.MoveType = _pmove.MoveType;
		player.MaxSpeed = _pmove.ClientMaxSpeed;
		player.StepLeft = _pmove.StepLeft;
		player.ViewOffset = _pmove.ViewOffset;
		player.MoveDir = _pmove.MoveDir;
		player.PunchAngle = _pmove.PunchAngle;

		if ( _pmove.OnGround == -1 )
		{
			player.EntityFlags &= ~EntityFlags.OnGround;
		}
		else
		{
			player.EntityFlags |= EntityFlags.OnGround;
			player.GroundEntity = GetEdict( _pmove.PhysEnts[_pmove.OnGround].Info );
		}

		player.Origin = _pmove.Origin;
		player.Velocity = _pmove.Velocity;
		player.BaseVelocity = _pmove.BaseVelocity;

		if ( player.FixAngle == 0 )
		{
			player.ViewAngle = _pmove.Angles;
			player.Angles = new Vector3( -_pmove.Angles.x / 3f, _pmove.Angles.y, _pmove.Angles.z );
		}

		player.InDuck = _pmove.InDuck;
		player.DuckTime = (int)_pmove.DuckTime;
		player.TimeStepSound = _pmove.TimeStepSound;
		player.FallVelocity = _pmove.FallVelocity;
		player.SwimTime = (int)_pmove.SwimTime;
		player.OldButtons = _pmove.Cmd.Buttons;

		player.IUser1 = _pmove.User1;
		player.IUser2 = _pmove.User2;
		player.IUser3 = _pmove.User3;
		player.IUser4 = _pmove.User4;
		player.FUser1 = _pmove.FloatUser1;
		player.FUser2 = _pmove.FloatUser2;
		player.FUser3 = _pmove.FloatUser3;
		player.FUser4 = _pmove.FloatUser4;
		player.VUser1 = _pmove.VectorUser1;
		player.VUser2 = _pmove.VectorUser2;
		player.VUser3 = _pmove.VectorUser3;
		player.VUser4 = _pmove.VectorUser4;
	}

	private readonly PhysEntPool _physEntPool = new();

	private void AddLinksToPlayerMove( Edict player, Vector3 origin )
	{
		var world = WorldBrushModel;

		_pmove.PhysEnts.Clear();
		_pmove.VisEnts.Clear();
		_pmove.MoveEnts.Clear();

		_physEntPool.Reset();

		var worldEnt = _physEntPool.Alloc();

		worldEnt.Name = world.Name;
		worldEnt.Model = world;
		worldEnt.Solid = Solid.Bsp;
		worldEnt.MoveType = MoveType.Push;

		_pmove.PhysEnts.Add( worldEnt );
		_pmove.VisEnts.Add( _pmove.PhysEnts[0] );

		AddLinksToPlayerMove( player, _areaNodes, origin - 256f, origin + 256f );
	}

	private void AddLinksToPlayerMove( Edict player, AreaNode node, Vector3 mins, Vector3 maxs )
	{
		foreach ( var check in node.SolidEdicts )
		{
			if ( check.Owner == player )
				continue;

			if ( check.Solid is not (Solid.Bsp or Solid.BBox or Solid.SlideBox or Solid.Not) )
				continue;

			var physEnt = CopyEdictToPhysEnt( check );
			_pmove.VisEnts.Add( physEnt );

			if ( check.Solid == Solid.Not && (check.Skin == 0 || check.ModelIndex == 0) )
				continue;

			if ( (check.EntityFlags & EntityFlags.MonsterClip) == EntityFlags.MonsterClip && check.Solid == Solid.Bsp )
				continue;

			if ( check == player )
				continue;

			if ( (check.EntityFlags & EntityFlags.Client) == EntityFlags.Client && check.Health <= 0f )
				continue;

			if ( check.Mins.z == 0f && check.Maxs.z == 1f )
				continue;

			if ( MathLib.Length( check.Size ) == 0f )
				continue;

			var absMin = check.AbsMin;
			var absMax = check.AbsMax;

			if ( (check.EntityFlags & EntityFlags.Client) == EntityFlags.Client && UseTruePosition( check.Index - 1 ) )
			{
				absMin = _truePositions[check.Index - 1].OldAbsMin;
				absMax = _truePositions[check.Index - 1].OldAbsMax;
			}

			if ( !Intersects( absMin, absMax, mins, maxs ) )
				continue;

			if ( check.Solid == Solid.Not && check.Skin == ContentsLadder )
			{
				if ( _pmove.MoveEnts.Count >= PlayerMove.MaxMoveEnts )
				{
					Con.DPrintf( "SV_AddLinksToPM:  pmove->nummoveent >= MAX_MOVEENTS\n" );
					continue;
				}

				_pmove.MoveEnts.Add( physEnt );
			}
			else
			{
				if ( _pmove.PhysEnts.Count >= PlayerMove.MaxPhysEnts )
				{
					Con.DPrintf( "SV_AddLinksToPM:  pmove->numphysent >= MAX_PHYSENTS\n" );
					return;
				}

				_pmove.PhysEnts.Add( physEnt );
			}
		}

		if ( node.Axis == -1 )
			return;

		if ( maxs[node.Axis] > node.Dist )
			AddLinksToPlayerMove( player, node.Children[0], mins, maxs );

		if ( mins[node.Axis] < node.Dist )
			AddLinksToPlayerMove( player, node.Children[1], mins, maxs );
	}

	private const int ContentsLadder = -16;

	private static bool Intersects( Vector3 absMin, Vector3 absMax, Vector3 mins, Vector3 maxs )
	{
		for ( var i = 0; i < 3; i++ )
		{
			if ( absMin[i] > maxs[i] || absMax[i] < mins[i] )
				return false;
		}

		return true;
	}

	private PhysEnt CopyEdictToPhysEnt( Edict edict )
	{
		var physEnt = _physEntPool.Alloc();

		physEnt.Info = edict.Index;
		physEnt.Origin = edict.Origin;
		physEnt.Player = edict.Index >= 1 && edict.Index <= MaxClients ? edict.Index : 0;
		physEnt.Angles = edict.Angles;
		physEnt.RenderMode = edict.RenderMode;
		physEnt.Solid = edict.Solid;
		physEnt.Skin = edict.Skin;
		physEnt.Frame = edict.Frame;
		physEnt.Sequence = edict.Sequence;
		physEnt.MoveType = edict.MoveType;

		if ( edict.Solid == Solid.Bsp || (edict.Solid == Solid.Not && edict.ModelIndex != 0) )
		{
			physEnt.Model = GetModel( edict.ModelIndex );
			physEnt.Name = physEnt.Model?.Name;
		}
		else if ( edict.Solid == Solid.BBox )
		{
			if ( GetStudioModel( edict.ModelIndex ) is { } studio )
			{
				if ( (studio.Header.Flags & StudioHulls.TraceHitBox) != 0 )
					physEnt.StudioModel = studio;

				physEnt.Name = edict.ModelName;
			}

			physEnt.Mins = edict.Mins;
			physEnt.Maxs = edict.Maxs;
		}
		else
		{
			physEnt.Mins = edict.Mins;
			physEnt.Maxs = edict.Maxs;
			physEnt.Name = edict.ClassName ?? "?";
		}

		if ( physEnt.Player != 0 && UseTruePosition( physEnt.Player - 1 ) )
			physEnt.Origin = _truePositions[physEnt.Player - 1].OldOrigin;

		edict.Controller.CopyTo( physEnt.Controller, 0 );
		edict.Blending.CopyTo( physEnt.Blending, 0 );

		return physEnt;
	}
}
