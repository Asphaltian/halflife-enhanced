using System;
using System.Globalization;

namespace GoldSrc;

partial class Client
{
	private const int MoveBufferSize = 0x800;
	private const int MaxCommandBackup = 8;
	private const int MaxNewCommands = 16;
	private const int MaxTotalCommands = 62;
	private const int NewCommandLimitBuild = 5971;
	private const int MaxLerpMsec = 100;
	private const double ConnectionProblemTime = 15.0;

	[ConVar( "cl_cmdbackup", ConVarFlags.Saved )]
	private static float CmdBackup { get; set; } = 2f;

	[ConVar( "cl_cmdrate", ConVarFlags.Saved )]
	private static float CmdRate { get; set; } = 60f;

	[ConVar( "ex_interp", ConVarFlags.Saved )]
	private static float ExInterp { get; set; } = 0.1f;

	private readonly SizeBuf _moveBuffer = new( "Client Move", MoveBufferSize, false );

	private double _packetLossTime;
	private float _packetLoss;
	private float _interpolation = 0.1f;
	private double _msecFraction;
	private float _nextCmdTime;
	private int _lastOutgoingCommand = -1;

	private void ComputePacketLoss()
	{
		if ( _packetLossTime > Host.RealTime )
			return;

		_packetLossTime = Host.RealTime + 1.0;

		var lost = 0;
		var count = 0;

		for ( var i = _netchan.IncomingSequence - UpdateBackup + 1; i <= _netchan.IncomingSequence; i++ )
		{
			count++;

			if ( _frames[i & UpdateMask] is { ReceivedTime: -1.0 } )
				lost++;
		}

		_packetLoss = count > 0 ? (float)(lost * 100.0 / count) : 0f;
	}

	private void ComputeInterpolation( ref UserCmd cmd )
	{
		var updateRate = Cvar.GetFloat( "cl_updaterate" );

		if ( updateRate < 10f )
		{
			Cvar.Set( "cl_updaterate", "20" );
			Con.Printf( $"cl_updaterate minimum is {10.0:F6}, resetting to default (20)\n" );

			updateRate = Cvar.GetFloat( "cl_updaterate" );
		}

		if ( updateRate > 102f )
		{
			Cvar.Set( "cl_updaterate", "102" );
			Con.Printf( $"cl_updaterate maximum is {102.0:F6}, resetting to maximum (102)\n" );

			updateRate = Cvar.GetFloat( "cl_updaterate" );
		}

		var maximum = MaxLerpMsec;
		var minimum = 50;

		if ( updateRate > 0f )
			minimum = Math.Max( (int)(1000f / updateRate), 1 );

		var msec = (int)(ExInterp * 1000f);
		var forced = false;

		if ( msec + 1 < minimum )
		{
			msec = minimum;
			forced = true;

			Con.Printf( $"ex_interp forced up to {msec} msec\n" );
		}
		else if ( msec - 1 > maximum )
		{
			msec = maximum;
			forced = true;

			Con.Printf( $"ex_interp forced down to {msec} msec\n" );
		}

		if ( forced )
			Cvar.Set( "ex_interp", (msec / 1000f).ToString( "F6", CultureInfo.InvariantCulture ) );

		msec = Math.Clamp( msec, minimum, maximum );

		var target = msec / 1000f;

		if ( target != _interpolation )
		{
			var difference = target - _interpolation;
			var step = (float)(Host.FrameTime * 0.05000000074505806);

			if ( difference > 0f )
				step = Math.Min( step, difference );
			else
				step = -Math.Min( step, -difference );

			_interpolation += step;
		}

		cmd.LerpMsec = (short)Math.Clamp( (int)(_interpolation * 1000f), 0, MaxLerpMsec );
	}

	private void Move()
	{
		if ( _state is ClientState.Dedicated or ClientState.Disconnected or ClientState.Connecting )
			return;

		if ( _state == ClientState.Connected )
		{
			_netchan.Transmit( null );
			return;
		}

		ComputePacketLoss();

		var backup = Math.Clamp( (int)CmdBackup, 0, MaxCommandBackup );
		var outgoing = _netchan.OutgoingSequence;

		ref var command = ref _commands[outgoing & UpdateMask];

		command.SentTime = (float)Host.RealTime;
		command.ReceivedTime = -1f;
		command.ProcessedFuncs = false;
		command.HeldBack = false;
		command.SendSize = 0;
		command.Cmd = default;

		SetSolidEntities();
		PushPMStates();
		SetSolidPlayers( _playerNum );

		_clientDll.CreateMove( (float)Host.FrameTime, ref command.Cmd, ref ViewAngles, _signon == SignOns );

		PopPMStates();

		ComputeInterpolation( ref command.Cmd );

		command.Cmd.LightLevel = _lightLevel;

		var buffer = _moveBuffer;

		buffer.Clear();
		buffer.WriteByte( Server.MoveCommand );

		var lengthOffset = buffer.CurSize;

		buffer.WriteByte( 0 );

		var checksumOffset = buffer.CurSize;

		buffer.WriteByte( 0 );
		buffer.WriteByte( Math.Clamp( (int)_packetLoss, 0, 100 ) );

		var msec = (int)(Host.FrameTime * 1000.0);

		_msecFraction += (Host.FrameTime * 1000.0) - msec;

		if ( _msecFraction > 1.0 )
		{
			_msecFraction -= 1.0;
			msec++;
		}

		msec = Math.Min( msec, 255 );

		command.Cmd.Msec = (byte)msec;

		_cmd = command.Cmd;

		if ( _demoPlayback )
			return;

		PredictMove( false );

		if ( CmdRate < 10f )
			Cvar.Set( "cl_cmdrate", "10" );

		if ( msec < 1 || (MaxClients != 1 && (!_netchan.Loopback || Cvar.GetFloat( "host_limitlocal" ) != 0f) && (Host.RealTime < _nextCmdTime || !_netchan.CanPacket())) )
		{
			_netchan.OutgoingSequence++;
			_commands[outgoing & UpdateMask].HeldBack = true;
			return;
		}

		_nextCmdTime = (float)(CmdRate > 0f ? (1f / CmdRate) + Host.RealTime : Host.RealTime);

		if ( _lastOutgoingCommand == -1 )
			_lastOutgoingCommand = outgoing;

		buffer.WriteByte( backup );

		var newCommands = outgoing - _lastOutgoingCommand;

		newCommands = Math.Min( newCommands, Host.BuildNumber() < NewCommandLimitBuild ? MaxNewCommands : MaxTotalCommands - backup );

		buffer.WriteByte( newCommands );

		var from = default( UserCmd );

		for ( var i = newCommands + backup - 1; i >= 0; i-- )
		{
			ref readonly var to = ref _commands[(outgoing - i) & UpdateMask].Cmd;

			buffer.WriteDeltaUsercmd( to, from );

			from = to;
		}

		var size = buffer.CurSize - checksumOffset - 1;
		var clamped = Math.Min( size, 255 );

		buffer.Data[lengthOffset] = (byte)clamped;
		buffer.Data[checksumOffset] = Common.BlockSequenceCrcByte( buffer.Data.AsSpan( lengthOffset + 2, size ), outgoing );

		Common.Munge( buffer.Data.AsSpan( lengthOffset + 2, clamped ), outgoing );

		if ( outgoing - _netchan.IncomingAcknowledged >= UpdateMask && Host.RealTime - _netchan.LastReceived > ConnectionProblemTime )
		{
			NXPrintf( 0, 1f, new Vector3( 1f, 0.2f, 0f ), "WARNING:  Connection Problem" );

			_validSequence = 0;
		}
		else if ( _validSequence != 0 && NoDelta == 0f && !(_demoRecording && _demoWaiting) )
		{
			buffer.WriteByte( Server.DeltaCommand );
			buffer.WriteByte( _validSequence & 0xFF );
		}

		_lastOutgoingCommand = outgoing;
		_commands[outgoing & UpdateMask].SendSize = buffer.CurSize;

		if ( _datagram.CurSize <= buffer.MaxSize - buffer.CurSize )
			buffer.Write( _datagram );

		_datagram.Clear();

		_netchan.Transmit( buffer );
	}
}
