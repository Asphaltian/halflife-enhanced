using System;

namespace GoldSrc;

partial class Server
{
	public const int MoveCommand = 2;
	public const int StringCommand = 3;
	public const int DeltaCommand = 4;

	private const int LastClientCommand = 11;
	private const int MaxCommandBackup = 63;
	private const int MaxDroppedCommands = 24;
	private const int MaxStringCommandLength = 127;
	private const int FullUpdateImpulse = 204;
	private const double FullUpdateInterval = 0.45;

	private static string[] ClientCommandNames =
	[
		"clc_bad", "clc_nop", "clc_move", "clc_stringcmd", "clc_delta", "clc_resourcelist", "clc_tmove",
		"clc_fileconsistency", "clc_voicedata", "clc_hltv", "clc_cvarvalue", "clc_cvarvalue2"
	];

	private static string[] ClientHostCommands =
	[
		"status", "god", "notarget", "fly", "name", "noclip", "kill", "pause", "spawn", "new", "sendres", "dropclient",
		"kick", "ping", "dlfile", "nextdl", "setinfo", "showinfo", "sendents", "fullupdate", "setpause", "unpause"
	];

	[ConVar( "sv_unlagsamples" )]
	private static float SvUnlagSamples { get; set; } = 1f;

	[ConVar( "sv_timeout" )]
	private static float SvTimeout { get; set; } = 60f;

	[ConVar( "clockwindow" )]
	private static float ClockWindow { get; set; } = 0.5f;

	private readonly MessageReader _netMessage = new();
	private readonly UserCmd[] _commands = new UserCmd[MaxCommandBackup + 1];
	private readonly float[] _lastFullUpdate = new float[MaxClientsLimit];
	private readonly SizeBuf _forceUpdate = new( "Force Update", 0x2400, false );

	private bool _alreadyMoved;
	private double _lastClockCheck;

	private void CheckCommandTimes()
	{
		if ( MaxClients == 1 || Host.RealTime - _lastClockCheck < 1.0 )
			return;

		_lastClockCheck = Host.RealTime;

		foreach ( var client in Clients )
		{
			if ( !client.Connected || !client.Active )
				continue;

			if ( client.ConnectTime == 0.0 )
				client.ConnectTime = Host.RealTime;

			var difference = (float)(client.ConnectTime + client.CmdTime - Host.RealTime);

			if ( difference > ClockWindow )
			{
				client.IgnoreCmdTime = ClockWindow + Host.RealTime;
				client.CmdTime = Host.RealTime - client.ConnectTime;
			}
			else if ( difference < -ClockWindow )
			{
				client.CmdTime = Host.RealTime - client.ConnectTime;
			}
		}
	}

	private void CheckTimeouts()
	{
		var dropTime = (float)(Host.RealTime - SvTimeout);

		foreach ( var client in Clients )
		{
			if ( client.FakeClient || (!client.Connected && !client.Active && !client.Spawned) )
				continue;

			if ( client.Netchan.LastReceived < dropTime )
			{
				Log.Info( $"{client.Name} timed out\n" );
				DropClient( client, false, "Timed out" );
			}
		}
	}

	private void ReadPackets()
	{
		foreach ( var client in Clients )
		{
			while ( (client.Connected || client.Active || client.Spawned) && client.Netchan.Incoming.TryDequeue( out var packet ) )
			{
				if ( client.Netchan.Process( packet, _netMessage ) )
				{
					if ( MaxClients == 1 )
						client.SendMessage = true;

					if ( !client.Active || !client.Spawned || !client.FullyConnected )
						client.SendMessage = true;

					ExecuteClientMessage( client );

					Globals.FrameTime = (float)Host.FrameTime;
				}

				if ( client.Netchan.IncomingReady && client.Netchan.CopyNormalFragments() is { } fragments )
				{
					_netMessage.BeginReading( fragments, fragments.Length );

					ExecuteClientMessage( client );
				}
			}
		}
	}

	private float CalcClientTime( ServerClient client )
	{
		var ping = 0f;
		var count = 0;
		var backtrack = Math.Min( Math.Max( (int)SvUnlagSamples, 1 ), Math.Min( UpdateBackup, 16 ) );

		for ( var i = 0; i < backtrack; i++ )
		{
			var frame = client.Frames[UpdateMask & (client.Netchan.IncomingAcknowledged - i)];

			if ( frame.PingTime <= 0f )
				continue;

			count++;
			ping += frame.PingTime;
		}

		if ( count == 0 )
			return 0f;

		var minPing = 9999f;
		var maxPing = -9999f;

		for ( var i = 0; i < Math.Min( UpdateBackup, 4 ); i++ )
		{
			var frame = client.Frames[UpdateMask & (client.Netchan.IncomingAcknowledged - i)];

			if ( frame.PingTime <= 0f )
				continue;

			if ( frame.PingTime > maxPing )
				maxPing = frame.PingTime;

			if ( frame.PingTime < minPing )
				minPing = frame.PingTime;
		}

		if ( minPing <= maxPing && Math.Abs( (double)(maxPing - minPing) ) > 0.2 )
			return 0f;

		return ping / count;
	}

	private void ExecuteClientMessage( ServerClient client )
	{
		_alreadyMoved = false;

		var frame = client.Frames[UpdateMask & client.Netchan.IncomingAcknowledged];
		var neverSent = frame.SentTime == 0.0;

		frame.PingTime = (float)(Host.RealTime - frame.SentTime - client.NextMessageInterval);

		if ( neverSent )
			frame.PingTime = 0f;

		if ( Host.RealTime - client.ConnectionStarted < 2.0 && frame.PingTime > 0f )
			frame.PingTime = 0f;

		client.Latency = CalcClientTime( client );

		_hostClient = client;

		client.DeltaSequence = -1;

		while ( true )
		{
			if ( _netMessage.BadRead )
			{
				Log.Info( "SV_ReadClientMessage: badread\n" );
				return;
			}

			var command = _netMessage.ReadByte();

			if ( command == -1 )
				return;

			if ( (uint)command > LastClientCommand )
			{
				Log.Info( $"SV_ReadClientMessage: unknown command char ({command})\n" );
				DropClient( client, false, "Bad command character in client command" );
				return;
			}

			switch ( command )
			{
				case 0:
				case 1:
				case 6:
					break;

				case MoveCommand:
					ParseMove( client );
					break;

				case StringCommand:
					ParseStringCommand( client );
					break;

				case DeltaCommand:
					client.DeltaSequence = _netMessage.ReadByte();
					break;

				case ResourceListCommand:
					ParseResourceList( client );
					break;

				default:
					throw new NotSupportedException( $"{ClientCommandNames[command]} is not ported" );
			}
		}
	}

	private void ParseStringCommand( ServerClient client )
	{
		var text = _netMessage.ReadString();

		Cmd.TokenizeString( text );

		foreach ( var name in ClientHostCommands )
		{
			if ( !Cmd.Argv( 0 ).Equals( name, StringComparison.OrdinalIgnoreCase ) )
				continue;

			if ( !Cmd.ExecuteClientString( text ) )
				throw new NotSupportedException( $"client command {name} is not ported" );

			return;
		}

		if ( text.Length > MaxStringCommandLength )
			text = text[..MaxStringCommandLength];

		Cmd.TokenizeString( text );

		GameDll.ClientCommand( GetEdict( client.EdictIndex ) );
	}

	private void EstablishTimeBase( ServerClient client, int dropped, int backup, int count )
	{
		var time = 0.0;

		if ( dropped < MaxDroppedCommands )
		{
			for ( ; dropped > backup; dropped-- )
				time += client.LastCmd.Msec / 1000.0;

			for ( ; dropped > 0; dropped-- )
				time += _commands[dropped + count - 1].Msec / 1000.0;
		}

		for ( var i = count - 1; i >= 0; i-- )
			time += _commands[i].Msec / 1000.0;

		client.SvTimeBase = Host.FrameTime + Time - time;
	}

	private void ParseMove( ServerClient client )
	{
		if ( _alreadyMoved )
		{
			_netMessage.BadRead = true;
			return;
		}

		_alreadyMoved = true;

		var message = _netMessage;
		var frame = client.Frames[UpdateMask & client.Netchan.IncomingAcknowledged];
		var start = message.ReadCount;
		var length = message.ReadByte();
		var checksum = (byte)message.ReadByte();

		if ( length <= 0 || start + 2 + length > message.Size )
		{
			Log.Info( $"SV_ParseMove: invalid length: {length}\n" );
			DropClient( client, false, "Invalid Move message" );

			message.BadRead = true;
			return;
		}

		Common.UnMunge( message.Data.AsSpan( start + 2, length ), client.Netchan.IncomingSequence );

		var packetLoss = message.ReadByte();

		client.VoiceLoopback = ((packetLoss >> 7) & 1) != 0;

		var backup = message.ReadByte();
		var count = message.ReadByte();

		NetChannel.Dropped += 1 - count;

		var total = count + backup;

		if ( (uint)total >= MaxCommandBackup )
		{
			Log.Info( $"SV_ReadClientMessage: too many cmds {total} sent for {client.Name}/{client.Address}\n" );
			DropClient( client, false, "CMD_MAXBACKUP hit" );

			message.BadRead = true;
			return;
		}

		var from = default( UserCmd );

		for ( var i = total - 1; i >= 0; i-- )
		{
			_commands[i] = message.ReadDeltaUsercmd( from );
			from = _commands[i];
		}

		if ( !Active || (!client.Active && !client.Spawned) )
			return;

		if ( message.BadRead )
		{
			Log.Info( $"Client {client.Name}:{client.Address} sent a bogus command packet\n" );
			return;
		}

		if ( Common.BlockSequenceCrcByte( message.Data.AsSpan( start + 2, message.ReadCount - start - 2 ), client.Netchan.IncomingSequence ) != checksum )
		{
			Log.Trace( $"Failed command checksum for {client.Name}:{client.Address}\n" );

			message.BadRead = true;
			return;
		}

		var player = GetEdict( client.EdictIndex );

		client.PacketLoss = packetLoss & 0x7F;

		if ( !Paused && (MaxClients > 1 || KeyDestGame) && (player.EntityFlags & EntityFlags.Frozen) != EntityFlags.Frozen )
		{
			player.ViewAngle = _commands[0].ViewAngles;
		}
		else
		{
			for ( var i = 0; i < count; i++ )
			{
				ref var cmd = ref _commands[i];

				cmd.Msec = 0;
				cmd.ForwardMove = 0f;
				cmd.SideMove = 0f;
				cmd.UpMove = 0f;
				cmd.Buttons = 0;

				if ( (player.EntityFlags & EntityFlags.Frozen) == EntityFlags.Frozen )
					cmd.Impulse = 0;

				cmd.ViewAngles = player.ViewAngle;
			}

			NetChannel.Dropped = 0;
		}

		player.Button = _commands[0].Buttons;
		player.LightLevel = _commands[0].LightLevel;

		EstablishTimeBase( client, NetChannel.Dropped, backup, count );

		if ( NetChannel.Dropped < MaxDroppedCommands )
		{
			for ( ; NetChannel.Dropped > backup; NetChannel.Dropped-- )
				RunCmd( client.LastCmd, 0 );

			for ( ; NetChannel.Dropped > 0; NetChannel.Dropped-- )
			{
				var index = NetChannel.Dropped + count - 1;

				RunCmd( _commands[index], client.Netchan.IncomingSequence - index );
			}
		}

		for ( var i = count - 1; i >= 0; i-- )
			RunCmd( _commands[i], client.Netchan.IncomingSequence - i );

		client.LastCmd = _commands[0];

		frame.PingTime = (float)(frame.PingTime - (client.LastCmd.Msec * 0.5 / 1000.0));

		if ( frame.PingTime < 0f )
			frame.PingTime = 0f;

		if ( player.AnimTime > Host.FrameTime + Time )
			player.AnimTime = (float)(Host.FrameTime + Time);
	}

	private bool ForceFullClientsUpdate( ServerClient client )
	{
		if ( !client.Active )
		{
			Log.Trace( $"{client.Name} is trying to fullupdate when not active at ({Time})\n" );
			return false;
		}

		var last = _lastFullUpdate[client.Slot];

		if ( Time < last )
		{
			last = 0f;
			_lastFullUpdate[client.Slot] = 0f;
		}

		var elapsed = Math.Max( (float)(Time - last), 0f );

		if ( elapsed < FullUpdateInterval && FullUpdateInterval < Time )
		{
			Log.Trace( $"{client.Name} is spamming fullupdate: ({Time}) ({last}) ({elapsed})\n" );
			return false;
		}

		_lastFullUpdate[client.Slot] = (float)Time;

		_forceUpdate.Clear();

		foreach ( var other in Clients )
		{
			if ( other == client || other.Active || other.Connected || other.Spawned )
				FullClientUpdate( other, _forceUpdate );
		}

		Log.Trace( $"Client {client.Name} started recording. Send full update.\n" );

		client.Netchan.CreateFragments( _forceUpdate );
		client.Netchan.FragSend();

		return true;
	}
}
