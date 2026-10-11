using System;

namespace GoldSrc;

partial class Server
{
	public const int TimeMessage = 7;
	public const int SetAngleMessage = 10;
	public const int ClientDataMessage = 15;
	public const int AddAngleMessage = 38;
	public const int ChokeMessage = 42;
	public const int PingsMessage = 17;

	[ConVar( "host_limitlocal" )]
	private static float HostLimitLocal { get; set; }

	[ConVar( "sv_failuretime" )]
	private static float SvFailureTime { get; set; } = 0.5f;

	private readonly SizeBuf _clientDatagram = new( "Client Datagram", DatagramSize, true );
	private readonly byte[] _nullClientData = new byte[LocalClientData.Size];
	private readonly byte[] _nullWeaponData = new byte[WeaponData.Size];
	private readonly WeaponData[] _weaponData = new WeaponData[ServerFrame.MaxWeaponData];

	private void SendClientMessages()
	{
		UpdateToReliableMessages();

		foreach ( var client in Clients )
		{
			_hostClient = client;

			if ( (!client.Active && !client.Connected && !client.Spawned) || client.FakeClient )
				continue;

			if ( client.SkipMessage )
			{
				client.SkipMessage = false;
				continue;
			}

			if ( HostLimitLocal == 0f && client.Loopback )
				client.SendMessage = true;

			if ( client.Active && client.Spawned && client.FullyConnected && client.NextMessageTime <= Host.FrameTime + Host.RealTime )
				client.SendMessage = true;

			if ( client.Message.Overflowed )
			{
				client.Message.Clear();
				client.Datagram.Clear();

				BroadcastPrintf( $"{client.Name} overflowed\n" );

				DropClient( client, false, "Reliable channel overflowed" );

				client.SendMessage = true;
				client.Netchan.ClearTime = 0.0;
			}
			else
			{
				if ( !client.SendMessage )
					continue;

				if ( SvFailureTime < Host.RealTime - client.Netchan.LastReceived )
					client.SendMessage = false;
			}

			if ( !client.SendMessage )
				continue;

			if ( !client.Netchan.CanPacket() )
			{
				client.ChokeCount++;
				continue;
			}

			client.SendMessage = false;
			client.NextMessageTime = Host.FrameTime + Host.RealTime + client.NextMessageInterval;

			if ( client.Active && client.Spawned && client.FullyConnected )
				SendClientDatagram( client );
			else
				client.Netchan.Transmit( null );
		}

		for ( var i = 1; i < _numEdicts; i++ )
		{
			if ( _edicts[i] is not { } entity )
				continue;

			entity.SkipInterpolation |= (entity.Effects & Effects.NoInterp) == Effects.NoInterp;
			entity.Effects &= ~(Effects.MuzzleFlash | Effects.NoInterp);
		}
	}

	private void SendClientDatagram( ServerClient client )
	{
		var message = _clientDatagram;

		message.Clear();

		message.WriteByte( TimeMessage );
		message.WriteFloat( (float)Time );

		WriteClientdataToMessage( client, message );
		WriteEntitiesToClient( client, message );

		if ( client.Datagram.Overflowed )
			Con.Printf( $"WARNING: datagram overflowed for {client.Name}\n" );
		else
			message.Write( client.Datagram );

		client.Datagram.Clear();

		if ( message.Overflowed )
		{
			Con.Printf( $"WARNING: msg overflowed for {client.Name}\n" );
			message.Clear();
		}

		client.Netchan.Transmit( message );
	}

	private void WriteClientdataToMessage( ServerClient client, SizeBuf message )
	{
		var edict = GetEdict( client.EdictIndex );
		var frame = client.Frames[client.Netchan.OutgoingSequence & UpdateMask];

		frame.SentTime = Host.RealTime;
		frame.PingTime = -1f;

		if ( client.ChokeCount != 0 )
		{
			message.WriteByte( ChokeMessage );
			client.ChokeCount = 0;
		}

		if ( edict.FixAngle != 0 )
		{
			if ( edict.FixAngle == 2 )
			{
				message.WriteByte( AddAngleMessage );
				message.WriteHiresAngle( edict.AVelocity.y );

				edict.AVelocity = new Vector3( edict.AVelocity.x, 0f, edict.AVelocity.z );
			}
			else
			{
				message.WriteByte( SetAngleMessage );
				message.WriteHiresAngle( edict.Angles.x );
				message.WriteHiresAngle( edict.Angles.y );
				message.WriteHiresAngle( edict.Angles.z );
			}

			edict.FixAngle = 0;
		}

		var data = new LocalClientData { PhysInfo = "" };

		GameDll.UpdateClientData( edict, _hostClient.LocalWeapons, ref data );

		data.Write( frame.ClientData );

		message.WriteByte( ClientDataMessage );

		if ( client.Proxy )
			return;

		message.StartBitWriting();

		ServerFrame from = null;

		if ( _hostClient.DeltaSequence == -1 )
		{
			message.WriteBits( 0, 1 );
		}
		else
		{
			message.WriteBits( 1, 1 );
			message.WriteBits( (uint)_hostClient.DeltaSequence, 8 );

			from = _hostClient.Frames[_hostClient.DeltaSequence & UpdateMask];
		}

		_clientDelta.WriteDelta( message, from?.ClientData ?? _nullClientData, frame.ClientData, true, Time );

		if ( _hostClient.LocalWeapons )
		{
			Array.Clear( _weaponData );

			if ( GameDll.GetWeaponData( edict, _weaponData ) )
			{
				for ( var i = 0; i < ServerFrame.MaxWeaponData; i++ )
				{
					var to = frame.WeaponData[i];
					var old = from?.WeaponData[i] ?? _nullWeaponData;

					_weaponData[i].Write( to );

					if ( _weaponDelta.CheckDelta( old, to ) == 0 )
						continue;

					message.WriteBits( 1, 1 );
					message.WriteBits( (uint)i, 6 );

					_weaponDelta.WriteDelta( message, old, to, true, Time );
				}
			}
		}

		message.WriteBits( 0, 1 );
		message.EndBitWriting();
	}
}
