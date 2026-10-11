using System;

namespace GoldSrc;

partial class Client
{
	private const int MaxQueuedEvents = 64;

	private const int EventReliable = 1 << 1;

	private readonly List<(double FireTime, QueuedEvent Event)> _events = [];
	private readonly byte[] _eventImage = new byte[EventArgs.Size];
	private readonly byte[] _nullEventImage = new byte[EventArgs.Size];

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
				Con.DPrintf( "CL_ParseEvent:  Received non-packet entity index 0 for event\n" );
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
}
