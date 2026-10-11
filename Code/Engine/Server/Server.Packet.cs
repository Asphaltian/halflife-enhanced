namespace GoldSrc;

public sealed class PacketEntities
{
	public const int MaxEntities = 1024;

	public int Count;
	public readonly EntityState[] Entities = new EntityState[MaxEntities];
	public readonly byte[] Flags = new byte[MaxEntities / 8];
}

partial class Server
{
	public const int MaxPacketEntities = GoldSrc.PacketEntities.MaxEntities;

	public const int PacketEntitiesMessage = 40;
	public const int DeltaPacketEntitiesMessage = 41;
	public const int SpawnBaselineMessage = 22;

	private const int SinglePlayerBackup = 8;
	private const int MultiPlayerBackup = 64;
	private const int MaxDeltaOffset = 63;
	private const int MaxPacketOffset = 62;
	private const string PlayerModelName = "models/player.mdl";

	private readonly byte[] _fromImage = new byte[EntityState.Size];
	private readonly byte[] _toImage = new byte[EntityState.Size];
	private readonly byte[] _nullImage = new byte[EntityState.Size];
	private readonly byte[] _candidateImage = new byte[EntityState.Size];

	private readonly List<(string ClassName, EntityState State)> _instancedBaselines = [];

	private EntityState[] _baselines = [];
	private int _playerModel;
	private int _lastBaseline;
	private int _deltaNumBase;

	[ConVar( "sv_instancedbaseline" )]
	private static float SvInstancedBaseline { get; set; } = 1f;

	public int UpdateBackup => MaxClients == 1 ? SinglePlayerBackup : MultiPlayerBackup;

	public int UpdateMask => UpdateBackup - 1;

	private Delta EntityDelta( int entityType, int number )
	{
		if ( (entityType & EntityState.EntityBeam) != 0 )
			return _customEntityDelta;

		return number >= 1 && number <= MaxClients ? _playerDelta : _entityDelta;
	}

	private void CreateBaseline()
	{
		_baselines = new EntityState[_maxEdicts];
		_instancedBaselines.Clear();
		_playerModel = FindModel( PlayerModelName );

		for ( var index = 0; index < _numEdicts; index++ )
		{
			if ( _edicts[index] is not { } edict || (index > MaxClients && edict.ModelIndex == 0) )
				continue;

			ref var baseline = ref _baselines[index];

			baseline = default;
			baseline.Number = index;
			baseline.EntityType = (edict.EntityFlags & EntityFlags.CustomEntity) == EntityFlags.CustomEntity ? EntityState.EntityBeam : EntityState.EntityNormal;

			GameDll.CreateBaseline( index >= 1 && index <= MaxClients, index, ref baseline, edict, _playerModel, PlayerMove.PlayerMins[0], PlayerMove.PlayerMaxs[0] );

			_lastBaseline = index;
		}

		GameDll.CreateInstancedBaselines();

		_signon.WriteByte( SpawnBaselineMessage );
		_signon.StartBitWriting();

		for ( var index = 0; index < _numEdicts; index++ )
		{
			if ( _edicts[index] is not { } edict || (index > MaxClients && edict.ModelIndex == 0) )
				continue;

			ref var baseline = ref _baselines[index];

			_signon.WriteBits( (uint)index, 11 );
			_signon.WriteBits( (uint)baseline.EntityType, 2 );

			baseline.Write( _toImage );

			var delta = (baseline.EntityType & EntityState.EntityNormal) == 0 ? _customEntityDelta : index >= 1 && index <= MaxClients ? _playerDelta : _entityDelta;

			delta.WriteDelta( _signon, _nullImage, _toImage, true, Time );
		}

		_signon.WriteBits( 0xFFFF, 16 );
		_signon.WriteBits( (uint)_instancedBaselines.Count, 6 );

		foreach ( var (_, state) in _instancedBaselines )
		{
			state.Write( _toImage );

			_entityDelta.WriteDelta( _signon, _nullImage, _toImage, true, Time );
		}

		_signon.EndBitWriting();
	}

	private void WriteEntitiesToClient( ServerClient client, SizeBuf message )
	{
		var host = GetEdict( client.EdictIndex );
		var frame = client.Frames[client.Netchan.OutgoingSequence & UpdateMask];
		var pack = frame.Entities;
		var pvs = GameDll.SetupVisibility( ViewEntity, host );
		var hostFlags = client.LocalWeapons ? 1 : 0;
		bool sendPings;

		if ( !client.Proxy )
		{
			sendPings = (client.LastCmd.Buttons & (ushort)InButtons.Score) != 0;
		}
		else if ( Host.RealTime < client.NextPing )
		{
			sendPings = false;
		}
		else
		{
			sendPings = true;

			client.NextPing = Host.RealTime + PingInterval;
		}

		_hostClient = client;

		pack.Count = 0;

		for ( var i = 1; i < _numEdicts; i++ )
		{
			var player = i >= 1 && i <= MaxClients;

			if ( player )
			{
				var other = _clients[i - 1];

				if ( (!other.Active && !other.Spawned) || other.Proxy )
					continue;
			}

			if ( pack.Count >= MaxPacketEntities )
			{
				Con.DPrintf( "Too many entities in visible packet list.\n" );
				break;
			}

			if ( _edicts[i] is not { } entity )
				continue;

			if ( GameDll.AddToFullPack( ref pack.Entities[pack.Count], i, entity, host, hostFlags, player, pvs ) )
				pack.Count++;
		}

		EmitPacketEntities( client.DeltaSequence != -1, client, pack, message );
		EmitEvents( client, pack, message );

		if ( sendPings )
			EmitPings( message );
	}

	private const double PingInterval = 2.0;

	private static int[] _pingCache = new int[MaxClientsLimit];
	private static byte[] _lossCache = new byte[MaxClientsLimit];

	private void EmitPings( SizeBuf message )
	{
		message.WriteByte( PingsMessage );
		message.StartBitWriting();

		for ( var i = 0; i < MaxClients; i++ )
		{
			var client = _clients[i];

			if ( !client.Active )
				continue;

			if ( client.NextPing <= Host.RealTime )
			{
				client.NextPing = Host.RealTime + PingInterval;

				_pingCache[i] = CalcPing( client );
				_lossCache[i] = (byte)(int)client.PacketLoss;
			}

			message.WriteBits( 1, 1 );
			message.WriteBits( (uint)i, 5 );
			message.WriteBits( (uint)_pingCache[i], 12 );
			message.WriteBits( _lossCache[i], 7 );
		}

		message.WriteBits( 0, 1 );
		message.EndBitWriting();
	}

	private void WriteDeltaHeader( SizeBuf message, int number, bool remove, bool custom, bool instanced, int instancedIndex, bool full, int offset )
	{
		var delta = number - _deltaNumBase;

		if ( full && delta == 1 )
		{
			message.WriteBits( 1, 1 );
		}
		else
		{
			message.WriteBits( !full && remove ? 1u : 0u, 1 );

			if ( (uint)(delta - 1) < MaxDeltaOffset )
			{
				message.WriteBits( 0, 1 );
				message.WriteBits( (uint)delta, 6 );
			}
			else
			{
				message.WriteBits( 1, 1 );
				message.WriteBits( (uint)number, 11 );
			}
		}

		_deltaNumBase = number;

		if ( remove )
			return;

		message.WriteBits( custom ? 1u : 0u, 1 );

		if ( _instancedBaselines.Count != 0 )
		{
			if ( instanced )
			{
				message.WriteBits( 1, 1 );
				message.WriteBits( (uint)instancedIndex, 6 );
			}
			else
			{
				message.WriteBits( 0, 1 );
			}
		}

		if ( !full || instanced )
			return;

		if ( offset != 0 )
		{
			message.WriteBits( 1, 1 );
			message.WriteBits( (uint)offset, 6 );
		}
		else
		{
			message.WriteBits( 0, 1 );
		}
	}

	private sealed class DeltaHeader( Server server, SizeBuf message )
	{
		public int Number;
		public bool Custom;
		public bool Instanced;
		public int InstancedIndex;
		public bool Full;
		public int Offset;

		public void Write() => server.WriteDeltaHeader( message, Number, false, Custom, Instanced, InstancedIndex, Full, Offset );
	}

	private int EmitPacketEntities( bool delta, ServerClient client, PacketEntities to, SizeBuf message )
	{
		PacketEntities from = null;
		var oldMax = 0;

		if ( delta )
		{
			from = client.Frames[client.DeltaSequence & UpdateMask].Entities;
			oldMax = from.Count;

			message.WriteByte( DeltaPacketEntitiesMessage );
			message.WriteShort( to.Count );
			message.WriteByte( client.DeltaSequence );
		}
		else
		{
			message.WriteByte( PacketEntitiesMessage );
			message.WriteShort( to.Count );
		}

		var header = new DeltaHeader( this, message );
		var newIndex = 0;
		var oldIndex = 0;

		_deltaNumBase = 0;

		message.StartBitWriting();

		while ( newIndex < to.Count || oldIndex < oldMax )
		{
			var newNumber = newIndex < to.Count ? to.Entities[newIndex].Number : 9999;
			var oldNumber = oldIndex < oldMax ? from.Entities[oldIndex].Number : 9999;

			if ( newNumber == oldNumber )
			{
				ref var state = ref to.Entities[newIndex];

				header.Number = newNumber;
				header.Custom = (state.EntityType & EntityState.EntityBeam) != 0;
				header.Instanced = false;
				header.InstancedIndex = 0;
				header.Full = false;
				header.Offset = 0;

				from.Entities[oldIndex].Write( _fromImage );
				state.Write( _toImage );

				EntityDelta( state.EntityType, newNumber ).WriteDelta( message, _fromImage, _toImage, false, Time, header.Write );

				newIndex++;
				oldIndex++;
				continue;
			}

			if ( newNumber < oldNumber )
			{
				ref var state = ref to.Entities[newIndex];
				var custom = (state.EntityType & EntityState.EntityBeam) != 0;
				var entityDelta = EntityDelta( state.EntityType, newNumber );

				header.Number = newNumber;
				header.Custom = custom;
				header.Instanced = false;
				header.InstancedIndex = 0;
				header.Full = from is null;
				header.Offset = 0;

				state.Write( _toImage );
				_baselines[newNumber].Write( _fromImage );

				if ( SvInstancedBaseline != 0f && _instancedBaselines.Count != 0 && newNumber > _lastBaseline )
				{
					var className = GetEdict( newNumber )?.ClassName;

					for ( var i = 0; i < _instancedBaselines.Count; i++ )
					{
						if ( _instancedBaselines[i].ClassName != className )
							continue;

						header.Instanced = true;
						header.InstancedIndex = i;

						_instancedBaselines[i].State.Write( _fromImage );
						break;
					}
				}
				else if ( from is null )
				{
					var best = newIndex;
					var bestBits = entityDelta.TestDelta( _fromImage, _toImage ) - 6;

					for ( var i = newIndex - 1; bestBits > 0 && i >= 0 && newIndex - i <= MaxPacketOffset; i-- )
					{
						if ( to.Entities[i].EntityType != state.EntityType )
							continue;

						to.Entities[i].Write( _candidateImage );

						var bits = entityDelta.TestDelta( _candidateImage, _toImage );

						if ( bits < bestBits )
						{
							bestBits = bits;
							best = i;
						}
					}

					if ( newIndex - best != 0 )
					{
						header.Offset = newIndex - best;

						to.Entities[best].Write( _fromImage );
					}
				}

				entityDelta.WriteDelta( message, _fromImage, _toImage, true, Time, header.Write );

				newIndex++;
				continue;
			}

			WriteDeltaHeader( message, oldNumber, true, false, false, 0, false, 0 );

			oldIndex++;
		}

		message.WriteBits( 0, 16 );
		message.EndBitWriting();

		return message.CurSize;
	}
}
