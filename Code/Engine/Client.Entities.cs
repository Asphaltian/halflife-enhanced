using System;

namespace GoldSrc;

partial class Client
{
	private const int UpdateBackup = 64;
	private const int UpdateMask = UpdateBackup - 1;
	private const int MaxInstancedBaselines = 64;
	private const int NoEntity = 9999;

	private readonly ClientFrame[] _frames = new ClientFrame[UpdateBackup];
	private readonly PacketEntities _emptyPacket = new();
	private readonly EntityState[] _instancedBaselines = new EntityState[MaxInstancedBaselines];
	private readonly byte[] _fromImage = new byte[EntityState.Size];
	private readonly byte[] _toImage = new byte[EntityState.Size];
	private readonly byte[] _nullImage = new byte[EntityState.Size];

	private const int MaxVisibleEntities = 512;
	private const int MaxBeamEntityList = 64;

	private readonly List<ClientEntity> _entities = [];
	private readonly List<ClientEntity> _visibleEntities = [];
	private readonly List<ClientEntity> _beamEntities = [];

	private EntityState[] _baselines = [];
	private int _instancedBaselineCount;
	private int _validSequence;

	public ClientFrame CurrentFrame => GetFrame( _parseCountMod );

	public IReadOnlyList<ClientEntity> VisibleEntities => _visibleEntities;

	private bool IsPlayerIndex( int index ) => index >= 1 && index <= MaxClients;

	public ClientEntity GetEntity( int index )
	{
		return index >= 0 && index < _entities.Count ? _entities[index] : null;
	}

	public ClientEntity GetCurrentEntity( int index )
	{
		return GetEntity( index ) is { } entity && entity.CurState.MessageNum == _parseCount ? entity : null;
	}

	private ClientEntity CreateEntity( int index )
	{
		while ( _entities.Count <= index )
			_entities.Add( null );

		if ( _entities[index] is { } existing )
			return existing;

		var gameObject = new GameObject( true, $"entity {index}" );

		gameObject.NetworkMode = NetworkMode.Never;

		var entity = gameObject.AddComponent<ClientEntity>();

		entity.Index = index;

		return _entities[index] = entity;
	}

	public ClientEntity CreateClientEntity( string name )
	{
		var gameObject = new GameObject( true, name );

		gameObject.NetworkMode = NetworkMode.Never;

		var entity = gameObject.AddComponent<ClientEntity>();

		entity.ClientOwned = true;

		return entity;
	}

	public bool CreateVisibleEntity( int type, ClientEntity entity )
	{
		if ( type == ClientDll.EntityTypeBeam )
		{
			if ( _beamEntities.Count >= MaxBeamEntityList )
				return false;

			_beamEntities.Add( entity );

			return true;
		}

		if ( _visibleEntities.Count >= MaxVisibleEntities )
			return false;

		entity.SetRenderTransform();
		entity.Visible = true;

		_visibleEntities.Add( entity );

		return true;
	}

	private void ClearClientState()
	{
		foreach ( var entity in _entities )
		{
			if ( !entity.IsValid() )
				continue;

			entity.GameObject.Enabled = false;
			entity.GameObject.Destroy();
		}

		_entities.Clear();
		_visibleEntities.Clear();
		_beamEntities.Clear();

		Array.Clear( _frames );
		Array.Clear( _instancedBaselines );
		Array.Clear( _commands );
		Array.Clear( _predictedFrames );
		Array.Clear( _predictedOrigins );
		Array.Clear( _predictedPlayers );

		_baselines = [];
		_instancedBaselineCount = 0;
		_validSequence = 0;
		_parseCount = 0;
		_parseCountMod = 0;
		_simOrg = default;
		_simVel = default;
		_punchAngle = default;
		_viewHeight = default;
		_previousSimOrg = default;
		_previousViewHeight = default;
		_onGround = -1;
		_moving = false;
		_waterLevel = 0;
		_useHull = 0;
		_viewModelIndex = 0;
		_idealPitch = 0f;
		_health = 0;
		_weapons = 0;
		_maxSpeed = 0f;
		_pushMsec = 0;
		_physInfo = "";

		_resourcesNeeded.Clear();
		_resourcesOnHand.Clear();
		_customResources.Clear();
		_events.Clear();

		Array.Clear( _modelPrecache );
		Array.Clear( _soundPrecache );
		Array.Clear( _eventPrecache );

		ClearCustomizationLists( false );

		Array.Clear( _players );

		_modelPrecacheCount = 0;
		_maxClients = 0;
		_maxEdicts = 0;
		_playerNum = 0;
		_viewEntity = 0;
		_serverCount = 0;
		_serverCrc = 0;
		_mapCrc = 0;
		_levelName = "";
		_serverInfo = "";
		_intermission = 0;
		_paused = false;
		_messageTime = 0.0;
		_oldMessageTime = 0.0;
		_clientTime = 0.0;
		_clockDelta = 0.0;
		_oldClientTime = 0.0;
		_oldTime = 0.0;
		_crosshairAngle = default;

		ClientTime = 0.0;

		ViewAngles = default;

		_viewModel?.ClearState();

		RegisterLogo();
		CreateEntity( 0 );
	}

	private void ClearStickyEffects()
	{
		var pack = CurrentFrame.Entities;

		for ( var i = 0; i < pack.Count; i++ )
			GetEntity( pack.Entities[i].Number )?.ClearStickyEffects();
	}

	private void ProcessPacket( ClientFrame frame )
	{
		var pack = frame.Entities;

		for ( var i = 0; i < pack.Count; i++ )
		{
			ref var state = ref pack.Entities[i];

			state.MessageNum = _parseCount;
			state.MsgTime = (float)_messageTime;

			var entity = CreateEntity( state.Number );
			var continuous = entity.CurState.MessageNum == _parseCount - 1;
			var player = IsPlayerIndex( state.Number );

			if ( state.Number - 1 == _playerNum )
				_clientDll.TxferLocalOverrides( ref state, LocalClientData.Read( frame.ClientData ) );

			var model = GetModelByIndex( state.ModelIndex );
			var newEntity = (pack.Flags[i >> 3] & (1 << (i & 7))) != 0;

			entity.ProcessUpdate( state, player, newEntity, continuous, GetStudioModel( state.ModelIndex ), model?.Name is ['*', ..], ClientTime );

			if ( !player )
				continue;

			ref var playerState = ref frame.PlayerState[state.Number - 1];

			playerState.Number = state.Number;
			playerState.MessageNum = _parseCount;
			playerState.MsgTime = (float)_messageTime;

			_clientDll.ProcessPlayerState( ref playerState, state );

			if ( state.Number - 1 == _playerNum )
				CheckPredictionError();
		}
	}

	private void LinkPacketEntities( float fraction )
	{
		_beamEntities.Clear();

		var pack = CurrentFrame.Entities;

		for ( var i = 0; i < pack.Count; i++ )
		{
			ref readonly var state = ref pack.Entities[i];

			if ( IsPlayerIndex( state.Number ) || state.ModelIndex == 0 || (state.Effects & (int)Effects.NoDraw) != 0 )
				continue;

			var entity = GetEntity( state.Number );
			var model = GetModelByIndex( state.ModelIndex );

			if ( model is null )
			{
				Log.Info( $"Tried to link edict {entity.Index} without model\n" );
				continue;
			}

			var studio = GetStudioModel( state.ModelIndex );

			var brush = model.Name is ['*', ..];

			entity.Link( studio, brush, ClientTime, _messageTime, FixModelInterpolation >= 1f );

			var moveType = (MoveType)((state.EntityType & EntityState.EntityNormal) != 0 ? entity.CurState.MoveType : entity.PacketState.MoveType);
			var parametric = entity.CurState.ImpactTime != 0f && entity.CurState.StartTime != 0f;
			var interpolated = brush || (!parametric && moveType is not (MoveType.None or MoveType.Walk or MoveType.Fly or MoveType.Step));

			if ( interpolated && !entity.ComputeInterpolated( brush, BrushModelInterpolation != 0f, _maxClients, _moving && _onGround == entity.Index, (float)(ClientTime - ExInterp) ) && !brush )
				continue;

			if ( (state.EntityType & EntityState.EntityNormal) == 0 )
			{
				if ( model.Type != ModelType.Sprite )
					throw HostError( "Bad model on beam ( not sprite ) " );

				if ( !_clientDll.AddEntity( ClientDll.EntityTypeBeam, entity, entity.Index, model.Name ) )
					continue;

				if ( _beamEntities.Count < MaxBeamEntityList )
					_beamEntities.Add( entity );
				else
					Log.Trace( "Overflow beam entity list!\n" );

				continue;
			}

			if ( !_clientDll.AddEntity( ClientDll.EntityTypeNormal, entity, entity.Index, model.Name ) )
				continue;

			if ( _visibleEntities.Count == MaxVisibleEntities )
				break;

			var direct = !_netchan.Loopback || (interpolated && _maxClients > 1);

			if ( direct )
				entity.SetRenderTransform();
			else
				entity.Interpolate( fraction );

			var origin = direct ? entity.Origin : entity.InterpolatedOrigin( fraction );

			LinkEntityEffects( entity, origin );

			if ( studio is not null )
				LinkEntityTrail( entity, studio.Header.Flags, origin );

			entity.SetAttachments( origin );
			entity.Visible = true;

			_visibleEntities.Add( entity );
		}

		foreach ( var entity in _visibleEntities )
		{
			if ( entity.CurState.AimEnt == 0 || entity.CurState.MoveType != (int)MoveType.Follow || GetEntity( entity.CurState.AimEnt ) is not { } aim )
				continue;

			entity.Origin = aim.Origin;
			entity.WorldPosition = aim.Visible ? aim.WorldPosition : aim.InterpolatedOrigin( fraction );
		}
	}

	private Delta EntityDelta( bool custom, int number )
	{
		if ( custom )
			return Delta.LookupRegistration( "custom_entity_state_t" );

		return Delta.LookupRegistration( IsPlayerIndex( number ) ? "entity_state_player_t" : "entity_state_t" );
	}

	private void EntityNum( int number )
	{
		var maxEdicts = _maxEdicts;

		if ( number >= maxEdicts )
			throw HostError( $"CL_EntityNum: {number} is an invalid number, cl.max_edicts is {maxEdicts}" );

		if ( _baselines.Length != maxEdicts )
			Array.Resize( ref _baselines, maxEdicts );
	}

	private void ParseBaseline( MessageReader message )
	{
		var time = _messageTime;

		message.StartBitReading();

		while ( message.PeekBits( 16 ) != 0xFFFF )
		{
			var number = (int)message.ReadBits( 11 );

			EntityNum( number );

			var type = (int)message.ReadBits( 2 );
			var delta = EntityDelta( (type & EntityState.EntityBeam) != 0, number );

			Array.Clear( _toImage );

			delta.ParseDelta( message, _nullImage, _toImage, time );

			_baselines[number] = EntityState.Read( _toImage );
			_baselines[number].EntityType = type;
		}

		message.ReadBits( 16 );

		Array.Clear( _instancedBaselines );

		_instancedBaselineCount = (int)message.ReadBits( 6 );

		var entityDelta = Delta.LookupRegistration( "entity_state_t" );

		for ( var i = 0; i < _instancedBaselineCount; i++ )
		{
			Array.Clear( _toImage );

			entityDelta.ParseDelta( message, _nullImage, _toImage, time );

			_instancedBaselines[i] = EntityState.Read( _toImage );
		}

		message.EndBitReading();
	}

	private int ParseDeltaHeader( MessageReader message, out bool remove, out bool custom, ref int numBase, out bool instanced, out int instancedIndex, bool full, out int offset )
	{
		int number;

		custom = false;
		instanced = false;
		instancedIndex = 0;
		offset = 0;

		bool absolute;

		if ( !full )
		{
			remove = message.ReadBits( 1 ) != 0;
			absolute = message.ReadBits( 1 ) != 0;
		}
		else
		{
			remove = false;

			if ( message.ReadBits( 1 ) != 0 )
			{
				number = numBase + 1;
				goto SetBase;
			}

			absolute = message.ReadBits( 1 ) != 0;
		}

		number = absolute ? (int)message.ReadBits( 11 ) : numBase + (int)message.ReadBits( 6 );

	SetBase:
		numBase = number;

		if ( remove )
			return number;

		custom = message.ReadBits( 1 ) != 0;

		if ( _instancedBaselineCount != 0 )
		{
			instanced = message.ReadBits( 1 ) != 0;

			if ( instanced )
				instancedIndex = (int)message.ReadBits( 6 );
		}

		if ( full && !instanced && message.ReadBits( 1 ) != 0 )
			offset = (int)message.ReadBits( 6 );

		return number;
	}

	private void FlushEntityPacket( MessageReader message, bool needBitEncodingFrame )
	{
		var time = _messageTime;
		var numBase = 0;

		_validSequence = 0;

		CurrentFrame.Invalid = true;

		if ( needBitEncodingFrame )
			message.StartBitReading();

		while ( true )
		{
			int number;
			var remove = false;
			var custom = false;

			if ( message.PeekBits( 16 ) == 0 )
			{
				number = (int)message.ReadBits( 16 );
			}
			else
			{
				remove = message.ReadBits( 1 ) != 0;

				number = message.ReadBits( 1 ) != 0 ? (int)message.ReadBits( 11 ) : numBase + (int)message.ReadBits( 6 );
				numBase = number;

				if ( !remove )
				{
					custom = message.ReadBits( 1 ) != 0;

					if ( _instancedBaselineCount != 0 && message.ReadBits( 1 ) != 0 )
						message.ReadBits( 6 );
				}
			}

			if ( message.BadRead )
				throw HostError( "msg_badread in packetentities\n" );

			if ( number == 0 )
				break;

			if ( !remove )
				EntityDelta( custom, number ).ParseDelta( message, _nullImage, _toImage, time );
		}

		if ( needBitEncodingFrame )
			message.EndBitReading();
	}

	private static void CheckPacketIndex( int index )
	{
		if ( index >= PacketEntities.MaxEntities )
			throw HostError( "CL_ParsePacketEntities: newindex == MAX_PACKET_ENTITIES" );
	}

	private int _playerEntityBits;

	private void ParsePacketEntities( MessageReader message, bool delta )
	{
		_playerEntityBits = 0;

		if ( _signon == 1 )
		{
			_signon = 2;

			SignonReply();
		}

		var time = _messageTime;
		var frame = CurrentFrame;
		var pack = frame.Entities;

		frame.Invalid = false;

		pack.Count = message.ReadShort();

		Array.Clear( pack.Flags );

		var from = delta ? message.ReadByte() & 0xFF : -1;
		var full = false;
		PacketEntities old;

		if ( from == -1 )
		{
			old = _emptyPacket;
			full = true;

			if ( _demoWaiting )
			{
				_demoWaiting = false;

				_netchan.Message.WriteByte( Server.StringCommand );
				_netchan.Message.WriteString( "fullupdate" );
			}
		}
		else
		{
			if ( ((_netchan.IncomingSequence - from) & 0xFF) >= UpdateMask )
			{
				FlushEntityPacket( message, true );

				pack.Count = 0;
				return;
			}

			old = (_frames[from & UpdateMask] ??= new ClientFrame()).Entities;
		}

		_validSequence = _netchan.IncomingSequence;

		var newIndex = 0;
		var oldIndex = 0;
		var numBase = 0;

		message.StartBitReading();

		while ( true )
		{
			int newNumber;
			var remove = false;
			var custom = false;
			var instanced = false;
			var instancedIndex = 0;
			var offset = 0;

			if ( message.PeekBits( 16 ) == 0 )
				newNumber = (int)message.ReadBits( 16 );
			else
				newNumber = ParseDeltaHeader( message, out remove, out custom, ref numBase, out instanced, out instancedIndex, full, out offset );

			if ( newNumber == 0 )
				break;

			var oldNumber = oldIndex < old.Count ? old.Entities[oldIndex].Number : NoEntity;

			while ( oldNumber < newNumber )
			{
				if ( full )
				{
					FlushEntityPacket( message, false );

					pack.Count = 0;
					goto Done;
				}

				CheckPacketIndex( newIndex );

				pack.Entities[newIndex++] = old.Entities[oldIndex++];

				oldNumber = oldIndex < old.Count ? old.Entities[oldIndex].Number : NoEntity;
			}

			if ( newNumber < oldNumber )
			{
				if ( remove )
				{
					if ( !full )
						continue;

					FlushEntityPacket( message, false );

					pack.Count = 0;
					goto Done;
				}

				CheckPacketIndex( newIndex );

				EntityNum( newNumber );

				if ( instanced )
					_instancedBaselines[instancedIndex].Write( _fromImage );
				else if ( offset != 0 )
					pack.Entities[newIndex - offset].Write( _fromImage );
				else
					_baselines[newNumber].Write( _fromImage );

				Array.Clear( _toImage );

				var bits = EntityDelta( custom, newNumber ).ParseDelta( message, _fromImage, _toImage, time );

				if ( newNumber > 0 && newNumber <= _maxClients )
					_playerEntityBits += bits;

				pack.Entities[newIndex] = EntityState.Read( _toImage );
				pack.Entities[newIndex].EntityType = custom ? EntityState.EntityBeam : EntityState.EntityNormal;
				pack.Entities[newIndex].Number = newNumber;

				pack.Flags[newIndex >> 3] |= (byte)(1 << (newIndex & 7));

				newIndex++;
				continue;
			}

			if ( full )
				_validSequence = 0;

			if ( remove )
			{
				KillDeadBeams( oldNumber );

				oldIndex++;
				continue;
			}

			old.Entities[oldIndex].Write( _fromImage );

			Array.Clear( _toImage );

			var lastBits = EntityDelta( custom, newNumber ).ParseDelta( message, _fromImage, _toImage, time );

			if ( newNumber > 0 && newNumber <= _maxClients )
				_playerEntityBits += lastBits;

			pack.Entities[newIndex] = EntityState.Read( _toImage );
			pack.Entities[newIndex].EntityType = custom ? EntityState.EntityBeam : EntityState.EntityNormal;
			pack.Entities[newIndex].Number = newNumber;

			newIndex++;
			oldIndex++;
		}

		while ( oldIndex < old.Count )
		{
			CheckPacketIndex( newIndex );

			pack.Entities[newIndex++] = old.Entities[oldIndex++];
		}

	Done:
		message.EndBitReading();

		if ( message.BadRead )
			throw HostError( "msg_badread in packetentities\n" );

		ProcessPacket( frame );
	}
}
