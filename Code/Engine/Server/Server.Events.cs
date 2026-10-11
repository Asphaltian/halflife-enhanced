using System;

namespace GoldSrc;

[Flags]
public enum EventFlags
{
	None = 0,
	NotHost = 1 << 0,
	Reliable = 1 << 1,
	Global = 1 << 2,
	Update = 1 << 3,
	HostOnly = 1 << 4,
	Server = 1 << 5,
	Client = 1 << 6
}

public struct EventArgs
{
	public const int HasOrigin = 1;
	public const int HasAngles = 2;

	public int Flags;
	public int EntIndex;
	public Vector3 Origin;
	public Vector3 Angles;
	public Vector3 Velocity;
	public int Ducking;
	public float FParam1;
	public float FParam2;
	public int IParam1;
	public int IParam2;
	public int BParam1;
	public int BParam2;

	public const int Size = 72;

	public readonly void Write( byte[] image )
	{
		StructImage.Write( image, 0, Flags );
		StructImage.Write( image, 4, EntIndex );
		StructImage.Write( image, 8, Origin );
		StructImage.Write( image, 20, Angles );
		StructImage.Write( image, 32, Velocity );
		StructImage.Write( image, 44, Ducking );
		StructImage.Write( image, 48, FParam1 );
		StructImage.Write( image, 52, FParam2 );
		StructImage.Write( image, 56, IParam1 );
		StructImage.Write( image, 60, IParam2 );
		StructImage.Write( image, 64, BParam1 );
		StructImage.Write( image, 68, BParam2 );
	}

	public static EventArgs Read( byte[] image )
	{
		return new EventArgs
		{
			Flags = StructImage.ReadInt( image, 0 ),
			EntIndex = StructImage.ReadInt( image, 4 ),
			Origin = StructImage.ReadVector( image, 8 ),
			Angles = StructImage.ReadVector( image, 20 ),
			Velocity = StructImage.ReadVector( image, 32 ),
			Ducking = StructImage.ReadInt( image, 44 ),
			FParam1 = StructImage.ReadFloat( image, 48 ),
			FParam2 = StructImage.ReadFloat( image, 52 ),
			IParam1 = StructImage.ReadInt( image, 56 ),
			IParam2 = StructImage.ReadInt( image, 60 ),
			BParam1 = StructImage.ReadInt( image, 64 ),
			BParam2 = StructImage.ReadInt( image, 68 )
		};
	}
}

public readonly record struct QueuedEvent( int Index, string Name, EventArgs Args, int Flags );

public struct EventInfo
{
	public short Index;
	public short PacketIndex;
	public short EntityIndex;
	public float FireTime;
	public EventArgs Args;
}

partial class Server
{
	public const int EventMessage = 3;
	public const int EventReliableMessage = 21;

	private const int MaxEvents = 256;
	private const int MaxEventsPerPacket = 31;
	private const int ReliableEventSize = 0x400;

	private readonly List<string> _events = [null];
	private readonly List<int> _eventSizes = [0];
	private readonly SizeBuf _reliableEvent = new( "Reliable Event", ReliableEventSize, false );
	private readonly byte[] _eventImage = new byte[EventArgs.Size];
	private readonly byte[] _nullEventImage = new byte[EventArgs.Size];

	public int EventIndex( string name )
	{
		var index = Common.IndexOfName( _events, name );

		return index > 0 ? index : 0;
	}

	public int PrecacheEvent( int type, string name )
	{
		if ( name is null )
			throw HostError( "EV_Precache: NULL pointer" );

		if ( name.Length == 0 || name[0] <= ' ' )
			throw HostError( $"EV_Precache: Bad string '{name}'" );

		var index = Common.IndexOfName( _events, name );

		if ( index > 0 )
			return index;

		if ( !_loading )
			throw HostError( $"EV_Precache: '{name}' Precache can only be done in spawn functions" );

		if ( _events.Count >= MaxEvents )
			throw HostError( $"EV_Precache: '{name}' overflow" );

		if ( type != 1 )
			throw HostError( "EV_Precache:  only file type 1 supported currently\n" );

		var size = FileSize( name );

		if ( size < 0 )
			throw HostError( $"EV_Precache:  file {name} missing from server\n" );

		_events.Add( name );
		_eventSizes.Add( size );

		return _events.Count - 1;
	}

	public void PlaybackEvent( EventFlags flags, Edict invoker, int eventIndex, float delay, Vector3 origin, Vector3 angles, float fparam1, float fparam2, int iparam1, int iparam2, int bparam1, int bparam2 )
	{
		if ( (flags & EventFlags.Client) == EventFlags.Client )
			return;

		var args = new EventArgs
		{
			FParam1 = fparam1,
			FParam2 = fparam2,
			IParam1 = iparam1,
			IParam2 = iparam2,
			BParam1 = bparam1,
			BParam2 = bparam2
		};

		if ( !MathLib.VectorCompare( origin, Vector3.Zero ) )
		{
			args.Origin = origin;
			args.Flags |= EventArgs.HasOrigin;
		}

		if ( !MathLib.VectorCompare( angles, Vector3.Zero ) )
		{
			args.Angles = angles;
			args.Flags |= EventArgs.HasAngles;
		}

		if ( eventIndex < 1 || eventIndex >= MaxEvents )
		{
			Con.DPrintf( $"EV_Playback:  index out of range {eventIndex}\n" );
			return;
		}

		if ( eventIndex >= _events.Count )
		{
			Con.DPrintf( $"EV_Playback:  no event for index {eventIndex}\n" );
			return;
		}

		var entityIndex = -1;
		Vector3 eventOrigin;

		if ( invoker is null )
		{
			eventOrigin = args.Origin;
		}
		else
		{
			eventOrigin = invoker.Origin;
			entityIndex = invoker.Index;

			if ( entityIndex > 0 && entityIndex <= MaxClients && (invoker.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
				args.Ducking = 1;

			if ( (args.Flags & EventArgs.HasOrigin) == 0 )
				args.Origin = invoker.Origin;

			if ( (args.Flags & EventArgs.HasAngles) == 0 )
				args.Angles = invoker.Angles;
		}

		var leaf = WorldModel.PointInLeaf( eventOrigin );

		foreach ( var client in Clients )
		{
			if ( !client.Active || !client.Spawned || !client.Connected || !client.FullyConnected || client.FakeClient )
				continue;

			if ( (flags & EventFlags.Global) != EventFlags.Global && invoker is not null && !ValidClientMulticast( client, leaf, MulticastPas ) )
				continue;

			if ( client == _hostClient && (flags & EventFlags.NotHost) == EventFlags.NotHost && client.LocalWeapons )
				continue;

			if ( (flags & EventFlags.HostOnly) == EventFlags.HostOnly && GetEdict( client.EdictIndex ) != invoker )
				continue;

			if ( (flags & EventFlags.Reliable) == EventFlags.Reliable )
			{
				WriteReliableEvent( client, eventIndex, delay, args with { EntIndex = invoker is null ? 0 : entityIndex } );
				continue;
			}

			var events = client.Events;
			var slot = -1;

			if ( (flags & EventFlags.Update) == EventFlags.Update )
			{
				for ( var i = 0; i < events.Length; i++ )
				{
					if ( events[i].Index == eventIndex && entityIndex != -1 && events[i].EntityIndex == entityIndex )
					{
						slot = i;
						break;
					}
				}
			}

			if ( slot == -1 )
			{
				for ( var i = 0; i < events.Length; i++ )
				{
					if ( events[i].Index == 0 )
					{
						slot = i;
						break;
					}
				}
			}

			if ( slot == -1 )
				continue;

			events[slot].Index = (short)eventIndex;
			events[slot].PacketIndex = -1;
			events[slot].EntityIndex = (short)(invoker is null ? -1 : entityIndex);
			events[slot].Args = args;
			events[slot].FireTime = delay;
		}
	}

	private void WriteReliableEvent( ServerClient client, int eventIndex, float delay, in EventArgs args )
	{
		var buffer = _reliableEvent;

		buffer.Clear();
		buffer.WriteByte( EventReliableMessage );
		buffer.StartBitWriting();
		buffer.WriteBits( (uint)eventIndex, 10 );

		args.Write( _eventImage );

		_eventDelta.WriteDelta( buffer, _nullEventImage, _eventImage, true, Time );

		if ( delay == 0f )
		{
			buffer.WriteBits( 0, 1 );
		}
		else
		{
			buffer.WriteBits( 1, 1 );
			buffer.WriteBits( (uint)(int)(delay * 100.0), 16 );
		}

		buffer.EndBitWriting();

		if ( client.Message.CurSize + buffer.CurSize > client.Message.MaxSize )
			client.Netchan.CreateFragments( buffer );
		else
			client.Message.Write( buffer );
	}

	private void EmitEvents( ServerClient client, PacketEntities pack, SizeBuf message )
	{
		var events = client.Events;
		var count = 0;

		for ( var i = 0; i < events.Length; i++ )
		{
			if ( events[i].Index != 0 )
				count++;
		}

		if ( count == 0 )
			return;

		if ( count > MaxEventsPerPacket )
			count = MaxEventsPerPacket;

		for ( var i = 0; i < events.Length; i++ )
		{
			ref var info = ref events[i];

			if ( info.Index == 0 )
				continue;

			var packetIndex = 0;

			while ( packetIndex < pack.Count && pack.Entities[packetIndex].Number != info.EntityIndex )
				packetIndex++;

			if ( packetIndex < pack.Count )
			{
				info.PacketIndex = (short)packetIndex;
				info.Args.Ducking = 0;

				if ( (info.Args.Flags & EventArgs.HasOrigin) == 0 )
					info.Args.Origin = Vector3.Zero;

				if ( (info.Args.Flags & EventArgs.HasAngles) == 0 )
					info.Args.Angles = Vector3.Zero;
			}
			else
			{
				info.PacketIndex = (short)pack.Count;
				info.Args.EntIndex = info.EntityIndex;
			}
		}

		message.WriteByte( EventMessage );
		message.StartBitWriting();
		message.WriteBits( (uint)count, 5 );

		var written = 0;

		for ( var i = 0; i < events.Length; i++ )
		{
			ref var info = ref events[i];

			if ( info.Index != 0 )
			{
				if ( written < count )
				{
					message.WriteBits( (uint)info.Index, 10 );

					if ( info.PacketIndex == -1 )
					{
						message.WriteBits( 0, 1 );
					}
					else
					{
						message.WriteBits( 1, 1 );
						message.WriteBits( (uint)info.PacketIndex, 11 );

						info.Args.Write( _eventImage );

						if ( _eventImage.AsSpan().SequenceEqual( _nullEventImage ) )
						{
							message.WriteBits( 0, 1 );
						}
						else
						{
							message.WriteBits( 1, 1 );

							_eventDelta.WriteDelta( message, _nullEventImage, _eventImage, true, Time );
						}
					}

					if ( info.FireTime == 0f )
					{
						message.WriteBits( 0, 1 );
					}
					else
					{
						message.WriteBits( 1, 1 );
						message.WriteBits( (uint)(int)(info.FireTime * 100.0), 16 );
					}
				}

				written++;
				info.Index = 0;
			}

			info.PacketIndex = -1;
			info.EntityIndex = -1;
		}

		message.EndBitWriting();
	}
}
