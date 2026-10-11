using System;
using System.Globalization;

namespace GoldSrc;

partial class Client
{
	private const int NetGraphTimings = 1024;
	private const int NetGraphTimingMask = NetGraphTimings - 1;
	private const int NetGraphLatencyDropped = 9999;
	private const int NetGraphLatencyInvalid = 9998;
	private const int NetGraphLatencySkipped = 9997;
	private const int NetGraphLatencyValid = 9996;
	private const int NetGraphLagColors = 24;
	private const float NetGraphLerpHeight = 24f;

	[ConVar( "net_graph", ConVarFlags.Saved )]
	private static float NetGraph { get; set; }

	[ConVar( "net_graphwidth" )]
	private static float NetGraphWidth { get; set; } = 150f;

	[ConVar( "net_scale", ConVarFlags.Saved )]
	private static float NetGraphScale { get; set; } = 5f;

	[ConVar( "net_graphpos", ConVarFlags.Saved )]
	private static float NetGraphPosition { get; set; } = 1f;

	[ConVar( "graphheight", ConVarFlags.Saved )]
	private static float GraphHeight { get; set; } = 64f;

	[ConVar( "cl_showfps", ConVarFlags.Saved )]
	private static float ShowFps { get; set; }

	private struct NetGraphPacket
	{
		public int Latency;
		public bool Choked;
		public ushort ClientBytes;
		public ushort PlayerInfoBytes;
		public ushort PacketEntityBytes;
		public ushort TentityBytes;
		public ushort SoundBytes;
		public ushort EventBytes;
		public ushort UsrBytes;
		public ushort MsgBytes;
		public ushort VoiceBytes;
	}

	private struct NetGraphCommand
	{
		public float Lerp;
		public int Size;
		public bool Sent;
	}

	[SkipHotload]
	private static readonly Color32[] NetGraphColors =
	[
		new( 255, 0, 0, 255 ),
		new( 0, 0, 255, 255 ),
		new( 240, 127, 63, 255 ),
		new( 255, 255, 0, 255 ),
		new( 63, 255, 63, 150 )
	];

	[SkipHotload]
	private static readonly byte[] NetGraphMinColor = [63, 0, 100, 255, 127, 0];
	[SkipHotload]
	private static readonly float[] NetGraphColorDelta = [-63f, 63f, 155f, -5f, -127f, 0f];

	private readonly NetGraphPacket[] _netGraphPackets = new NetGraphPacket[NetGraphTimings];
	private readonly NetGraphCommand[] _netGraphCommands = new NetGraphCommand[NetGraphTimings];
	private readonly Color32[] _netGraphLagColors = BuildNetGraphLagColors();
	private float _netGraphLoss;
	private float _netGraphChoke;
	private float _netGraphFrameRate;
	private int _netGraphLastOut;
	private double _showFpsRolling;

	private static Color32[] BuildNetGraphLagColors()
	{
		var colors = new Color32[NetGraphLagColors];

		for ( var i = 0; i < NetGraphLagColors; i++ )
		{
			var channels = new byte[3];

			for ( var j = 0; j < 3; j++ )
			{
				if ( i < 8 )
					channels[j] = (byte)(int)((i * 0.125f * NetGraphColorDelta[j]) + NetGraphMinColor[j]);
				else
					channels[j] = (byte)(int)(((i - 8) * 0.0625f * NetGraphColorDelta[j + 3]) + NetGraphMinColor[j + 3]);
			}

			colors[i] = new Color32( channels[0], channels[1], channels[2], 255 );
		}

		return colors;
	}

	private void NetGraphText( int x, int y, float r, float g, float b, string text )
	{
		DrawSetTextColor( r, g, b );
		DrawConsoleString( x, y, text );
	}

	private void DrawShowFps()
	{
		if ( ShowFps == 0f || Host.FrameTime <= 0.0 )
			return;

		_showFpsRolling = (Host.FrameTime * 0.4) + (_showFpsRolling * 0.6);

		NetGraphText( 2, 2, 1f, 1f, 1f, $"{(int)Math.Floor( 1.0 / _showFpsRolling )} fps" );
	}

	private void DrawNetGraph()
	{
		var graphType = (int)NetGraph;

		if ( graphType == 0 )
		{
			if ( _clientDll.FindKey( "in_graph" ) is not { } state || (state & 1) == 0 )
				return;

			graphType = 2;
		}

		if ( NetGraphScale <= 0f )
			NetGraphScale = 1f;

		var width = Math.Min( (int)NetGraphWidth, NetGraphTimings );

		if ( ScreenWidth < width + 10 )
			width = ScreenWidth - 10;

		var incoming = _netchan.IncomingSequence;
		var outgoing = _netchan.OutgoingSequence;
		var lossCount = 0;
		var chokeCount = 0;
		var latencyCount = 0;
		var latencySum = 0f;
		var maxMsgBytes = 0;

		for ( var i = incoming - UpdateBackup + 1; i <= incoming; i++ )
		{
			var frame = GetFrame( i );
			ref var packet = ref _netGraphPackets[i & NetGraphTimingMask];

			packet.Choked = frame.Choked;

			if ( frame.Choked )
				chokeCount++;

			if ( frame.Invalid )
			{
				packet.Latency = NetGraphLatencyInvalid;
			}
			else if ( frame.ReceivedTime == -1.0 )
			{
				lossCount++;
				packet.Latency = NetGraphLatencyDropped;
			}
			else if ( frame.ReceivedTime == -3.0 )
			{
				packet.Latency = NetGraphLatencySkipped;
			}
			else
			{
				var latency = Math.Min( frame.Latency, 1.0 );

				packet.Latency = (int)(((float)latency + 0.1) / 1.1 * (GraphHeight - NetGraphLerpHeight - 2f));

				if ( i > incoming - 8 )
				{
					latencyCount++;
					latencySum = (float)((frame.Latency * 1000.0) + latencySum);
				}
			}

			packet.ClientBytes = frame.ClientBytes;
			packet.PlayerInfoBytes = frame.PlayerInfoBytes;
			packet.PacketEntityBytes = frame.PacketEntityBytes;
			packet.TentityBytes = frame.TentityBytes;
			packet.SoundBytes = frame.SoundBytes;
			packet.EventBytes = frame.EventBytes;
			packet.UsrBytes = frame.UsrBytes;
			packet.MsgBytes = frame.MsgBytes;
			packet.VoiceBytes = frame.VoiceBytes;

			if ( frame.MsgBytes > maxMsgBytes )
				maxMsgBytes = frame.MsgBytes;
		}

		if ( maxMsgBytes > 1000 )
			maxMsgBytes = 1000;

		_netGraphLoss = (float)(((float)(lossCount * 100.0 / UpdateBackup) * 0.5) + (_netGraphLoss * 0.5));
		_netGraphChoke = (float)(((float)(chokeCount * 100.0 / UpdateBackup) * 0.5) + (_netGraphChoke * 0.5));

		for ( var i = outgoing - UpdateBackup + 1; i <= outgoing; i++ )
		{
			ref readonly var command = ref _commands[i & UpdateMask];
			ref var info = ref _netGraphCommands[i & NetGraphTimingMask];

			info.Lerp = command.FrameLerp;
			info.Sent = !command.HeldBack;
			info.Size = command.SendSize;
		}

		var x = 5;

		if ( (int)NetGraphPosition == 1 )
			x = ScreenWidth - width - 5;
		else if ( (int)NetGraphPosition == 2 )
			x = (ScreenWidth - width - 10) / 2;

		var y = ScreenHeight - 29;

		if ( graphType < 3 )
		{
			var lastValid = 0;
			var limit = GraphHeight - NetGraphLerpHeight - 2f;

			for ( var a = 0; a < width; a++ )
			{
				var column = x + width - 1 - a;
				ref readonly var packet = ref _netGraphPackets[(incoming - a) & NetGraphTimingMask];
				var valid = false;
				Color32 color;

				if ( packet.Latency == NetGraphLatencySkipped )
				{
					color = NetGraphColors[2];
				}
				else if ( packet.Latency == NetGraphLatencyInvalid )
				{
					color = NetGraphColors[1];
				}
				else if ( packet.Latency == NetGraphLatencyDropped )
				{
					color = NetGraphColors[0];
				}
				else
				{
					valid = true;
					color = NetGraphColors[packet.Choked ? 3 : 4];
				}

				if ( valid )
					lastValid = packet.Latency;

				var height = lastValid;

				if ( limit < height )
					height = (int)limit;

				var top = y - height;

				if ( valid )
				{
					height = 1;
				}
				else if ( height > 3 )
				{
					FillRgba( column, top, 1, 2, color.r, color.g, color.b, color.a );

					top = ScreenHeight - 31;
					height = 2;
				}

				FillRgba( column, top, 1, height, color.r, color.g, color.b, color.a );

				var edge = a < 4 || a >= width - 4;

				if ( edge )
					FillRgba( column, y, 1, 1, 0, 255, 0, 160 );

				if ( graphType <= 1 )
					continue;

				var usage = (int)(y - GraphHeight - 1f);

				if ( edge )
					FillRgba( column, usage, 1, 1, 255, 255, 255, 255 );

				if ( packet.Latency >= NetGraphLatencyValid )
					continue;

				usage = usage - (int)(packet.ClientBytes / NetGraphScale) - 1;

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 255, 0, 0, 128 );

				usage -= (int)(packet.PlayerInfoBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 255, 255, 0, 128 );

				usage -= (int)(packet.PacketEntityBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 255, 0, 255, 128 );

				usage -= (int)(packet.TentityBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 0, 0, 255, 128 );

				usage -= (int)(packet.SoundBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 0, 255, 0, 128 );

				usage -= (int)(packet.EventBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 0, 255, 255, 128 );

				usage -= (int)(packet.UsrBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 200, 200, 200, 128 );

				usage -= (int)(packet.VoiceBytes / NetGraphScale);

				if ( usage <= 1 )
					continue;

				FillRgba( column, usage, 1, 1, 255, 255, 255, 255 );

				usage = (int)(y - GraphHeight - 1f - (int)(packet.MsgBytes / NetGraphScale));

				if ( usage > 1 )
					FillRgba( column, usage, 1, 1, 240, 240, 240, 128 );
			}

			if ( graphType > 1 )
			{
				var step = Math.Max( 1, (int)(10.0 / NetGraphScale) );
				var start = (int)(y - GraphHeight - 1f);

				for ( var line = start; line > 0; line -= step )
				{
					var bytes = (start - line) * NetGraphScale;

					if ( maxMsgBytes + 50 <= bytes )
						break;

					if ( (int)bytes % 50 == 0 )
						FillRgba( x, line, 3, 1, 0, 200, 0, 255 );
					else if ( step > 5 )
						FillRgba( x, line, 3, 1, 63, 63, 0, 200 );
				}
			}

			NetGraphText( Math.Max( 1, x - 25 + width ), (int)MathF.Max( 1f, y - GraphHeight ), 0.9f, 0.9f, 0.7f, $"{(int)Common.Atof( UpdateRate )}/s" );

			DrawNetGraphLag( x, width, outgoing );
		}

		DrawNetGraphText( graphType, x, y, incoming, outgoing, latencyCount, latencySum );
	}

	private void DrawNetGraphLag( int x, int width, int outgoing )
	{
		NetGraphText( Math.Max( 1, x - 26 + width ), Math.Max( 1, ScreenHeight - 27 ), 0.9f, 0.9f, 0.7f, $"{(int)CmdRate}/s" );

		var bottom = ScreenHeight - 4;

		for ( var a = 0; a < width; a++ )
		{
			var column = x + width - 1 - a;
			ref readonly var info = ref _netGraphCommands[(outgoing - a) & NetGraphTimingMask];
			var height = (int)Math.Min( info.Lerp / 3.0 * NetGraphLerpHeight, NetGraphLerpHeight );

			if ( height < 8 )
			{
				var color = _netGraphLagColors[Math.Max( height, 0 )];

				FillRgba( column, bottom - height, 1, 1, color.r, color.g, color.b, 255 );
			}
			else
			{
				var color = _netGraphLagColors[height - 1];

				FillRgba( column, bottom - height + 1, 1, 1, color.r, color.g, color.b, 255 );
			}

			if ( a < 4 || a >= width - 4 )
				FillRgba( column, ScreenHeight - 12, 1, 1, 255, 255, 255, 255 );

			if ( !info.Sent )
				FillRgba( column, ScreenHeight - 3, 1, 1, 255, 0, 0, 200 );
		}
	}

	private void DrawNetGraphText( int graphType, int x, int y, int incoming, int outgoing, int latencyCount, float latencySum )
	{
		var ping = 0f;

		if ( latencyCount >= 1 )
		{
			ping = (float)((latencySum / latencyCount) - (Host.FrameTime * 0.5));

			var updateRate = Common.Atof( UpdateRate );

			if ( updateRate > 0f )
				ping -= (int)(1000f / updateRate);

			if ( ping < 0f )
				ping = 0f;
		}

		_netGraphFrameRate = (float)((_netGraphFrameRate * 0.5) + (Host.FrameTime * 0.5));

		var top = (int)(y - GraphHeight);

		if ( _netGraphFrameRate <= 0f )
			return;

		NetGraphText( x, top, 0.9f, 0.9f, 0.7f, string.Create( CultureInfo.InvariantCulture, $"{1.0 / _netGraphFrameRate:F1} fps" ) );

		if ( ping > 1f )
			NetGraphText( x + 75, top, 0.9f, 0.9f, 0.7f, $"{(int)ping} ms " );

		var outSize = _netGraphCommands[(outgoing - 1) & NetGraphTimingMask].Size;

		if ( outSize == 0 )
			outSize = _netGraphLastOut;

		_netGraphLastOut = outSize;

		var inSize = _netGraphPackets[incoming & NetGraphTimingMask].MsgBytes;

		NetGraphText( x, top + 15, 0.9f, 0.9f, 0.7f, string.Create( CultureInfo.InvariantCulture, $"in :  {inSize} {_netchan.Flows[NetChannel.FlowIncoming].AvgKBytesPerSec:F2} k/s" ) );
		NetGraphText( x, top + 30, 0.9f, 0.9f, 0.7f, string.Create( CultureInfo.InvariantCulture, $"out:  {outSize} {_netchan.Flows[NetChannel.FlowOutgoing].AvgKBytesPerSec:F2} k/s" ) );

		if ( graphType > 2 )
			NetGraphText( x, top + 45, 0.9f, 0.9f, 0.7f, $"loss: {(int)(_netGraphLoss + 0.49)} choke:  {(int)(_netGraphChoke + 0.49)}" );
	}
}
