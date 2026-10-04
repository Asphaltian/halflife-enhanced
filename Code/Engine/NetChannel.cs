using System;

namespace GoldSrc;

public sealed class NetChannel
{
	public const int MessageSize = 0x1766;

	private const int MaxPacketSize = 0x1795;
	private const int MaxRoutablePacket = 1400;
	private const int MaxReliablePayload = 1200;
	private const int FragmentBufferSize = 1400;
	private const int MinPacketSize = 16;
	private const int PacketHeaderSize = 8;
	private const int UdpHeaderSize = 28;
	private const int MaxStreams = 2;
	private const int NormalStream = 0;
	private const int MaxFragmentId = 25000;
	private const int MaxFragmentLength = 0x800;
	private const int MaxFragmentOffset = 0x4000;
	private const int NetMessageSize = 0x10000;
	private const double DefaultRate = 30000.0;
	private const uint ReliableBit = 1u << 31;
	private const uint FragmentBit = 1u << 30;

	private sealed class FragBuf
	{
		public int BufferId;
		public byte[] Data;
	}

	[ConVar( "net_chokeloop" )]
	private static float ChokeLoopback { get; set; }

	[ConVar( "sv_lan_rate" )]
	private static float LanRate { get; set; } = 20000f;

	public static int Dropped { get; set; }

	public bool Loopback;
	public string Address = "";
	public float LastReceived;
	public float ConnectTime;
	public double Rate = DefaultRate;
	public double ClearTime;

	public int IncomingSequence;
	public int IncomingAcknowledged;
	public int IncomingReliableAcknowledged;
	public int IncomingReliableSequence;
	public int OutgoingSequence = 1;
	public int ReliableSequence;
	public int LastReliableSequence;

	public const int FlowOutgoing = 0;
	public const int FlowIncoming = 1;

	private const int FlowLatent = 32;
	private const int FlowMask = FlowLatent - 1;
	private const double FlowInterval = 0.1;

	public sealed class Flow
	{
		public readonly int[] Size = new int[FlowLatent];
		public readonly double[] Time = new double[FlowLatent];
		public int Current;
		public double NextCompute;
		public float KBytesPerSec;
		public float AvgKBytesPerSec;

		public void Clear()
		{
			Array.Clear( Size );
			Array.Clear( Time );

			Current = 0;
			NextCompute = 0.0;
			KBytesPerSec = 0f;
			AvgKBytesPerSec = 0f;
		}
	}

	public readonly Flow[] Flows = [new(), new()];

	public readonly SizeBuf Message = new( "netchan->message", MessageSize, true );
	public readonly Queue<byte[]> Incoming = [];

	public Action<byte[]> Send;
	public Func<int> BlockSize;

	private readonly SizeBuf _send = new( "Netchan_Transmit", MaxPacketSize, false );
	private readonly byte[] _reliableBuffer = new byte[MessageSize];
	private readonly Queue<Queue<FragBuf>>[] _waitList = [[], []];
	private readonly Queue<FragBuf>[] _fragBufs = new Queue<FragBuf>[MaxStreams];
	private readonly int[] _fragBufCount = new int[MaxStreams];
	private readonly bool[] _reliableFragment = new bool[MaxStreams];
	private readonly int[] _reliableFragId = new int[MaxStreams];
	private readonly int[] _fragStartPos = new int[MaxStreams];
	private readonly int[] _fragLength = new int[MaxStreams];
	private readonly List<FragBuf>[] _incomingBufs = [[], []];
	private readonly bool[] _incomingReady = new bool[MaxStreams];

	private bool _client;
	private int _reliableLength;

	public void Setup( bool client, bool loopback, string address, Action<byte[]> send, Func<int> blockSize )
	{
		Clear();

		Message.Clear();

		_client = client;

		Loopback = loopback;
		Address = address;
		Send = send;
		BlockSize = blockSize;
		LastReceived = (float)Host.RealTime;
		ConnectTime = (float)Host.RealTime;
		Rate = DefaultRate;

		IncomingSequence = 0;
		IncomingAcknowledged = 0;
		IncomingReliableAcknowledged = 0;
		IncomingReliableSequence = 0;
		OutgoingSequence = 1;
		ReliableSequence = 0;
		LastReliableSequence = 0;

		Flows[FlowOutgoing].Clear();
		Flows[FlowIncoming].Clear();

		Incoming.Clear();
	}

	private void AddFlow( int direction, int size )
	{
		var flow = Flows[direction];

		flow.Size[flow.Current & FlowMask] = size + UdpHeaderSize;
		flow.Time[flow.Current & FlowMask] = Host.RealTime;
		flow.Current++;

		UpdateFlow();
	}

	private void UpdateFlow()
	{
		var bytes = 0;
		var time = 0f;

		foreach ( var flow in Flows )
		{
			if ( Host.RealTime - flow.NextCompute < FlowInterval )
				continue;

			flow.NextCompute = Host.RealTime + FlowInterval;

			var start = flow.Current - 1;
			var previous = start & FlowMask;

			for ( var i = 0; i < 15; i++ )
			{
				var index = (start - 1 - i) & FlowMask;

				bytes += flow.Size[index];
				time = (float)(flow.Time[previous] - flow.Time[index] + time);

				previous = index;
			}

			flow.KBytesPerSec = time == 0f ? 0f : (float)(bytes / time * 0.0009765625);
			flow.AvgKBytesPerSec = (float)((flow.AvgKBytesPerSec * 0.6666666666666666) + (flow.KBytesPerSec * 0.33333333333333337));
		}
	}

	public void Clear()
	{
		for ( var i = 0; i < MaxStreams; i++ )
		{
			_waitList[i].Clear();
			_fragBufs[i] = null;
			_incomingBufs[i].Clear();
			_incomingReady[i] = false;
		}

		if ( _reliableLength != 0 )
		{
			Log.Trace( $"Netchan_Clear() : reliable length not 0, reliable_sequence: {_reliableLength}, incoming_reliable_acknowledged: {IncomingReliableAcknowledged}\n" );

			_reliableLength = 0;
			ReliableSequence ^= 1;
		}

		ClearTime = 0.0;

		for ( var i = 0; i < MaxStreams; i++ )
		{
			_reliableFragId[i] = 0;
			_reliableFragment[i] = false;
			_fragBufCount[i] = 0;
			_fragStartPos[i] = 0;
			_fragLength[i] = 0;
		}
	}

	public bool CanPacket()
	{
		if ( ChokeLoopback == 0f && Loopback )
		{
			ClearTime = Host.RealTime;
			return true;
		}

		return ClearTime < Host.RealTime;
	}

	private void CreateFragmentBuffers( SizeBuf message )
	{
		if ( message.CurSize == 0 )
			return;

		var chunkSize = BlockSize();
		var wait = new Queue<FragBuf>();
		var remaining = message.CurSize;
		var position = 0;
		var bufferId = 1;

		while ( remaining > 0 )
		{
			var bytes = Math.Min( remaining, chunkSize );

			remaining -= bytes;

			wait.Enqueue( new FragBuf { BufferId = bufferId++, Data = message.Data.AsSpan( position, bytes ).ToArray() } );

			position += bytes;
		}

		_waitList[NormalStream].Enqueue( wait );
	}

	public void CreateFragments( SizeBuf message )
	{
		if ( Message.CurSize > 0 )
		{
			CreateFragmentBuffers( Message );

			Message.CurSize = 0;
		}

		CreateFragmentBuffers( message );
	}

	public void FragSend()
	{
		for ( var i = 0; i < MaxStreams; i++ )
		{
			if ( _fragBufs[i] is not null || !_waitList[i].TryDequeue( out var wait ) )
				continue;

			_fragBufs[i] = wait;
			_fragBufCount[i] = wait.Count;
		}
	}

	public void Transmit( SizeBuf data )
	{
		var send = _send;

		send.Clear();

		if ( Message.Overflowed )
		{
			Log.Info( $"{Address}:Outgoing message overflow\n" );
			return;
		}

		var resending = IncomingAcknowledged > LastReliableSequence && IncomingReliableAcknowledged != ReliableSequence;
		var sendReliable = resending;

		if ( _reliableLength == 0 )
		{
			FragSend();

			var sendFromRegular = Message.CurSize != 0;
			var sendFromFragment = false;

			if ( sendFromRegular && _fragBufs[NormalStream] is not null )
			{
				sendFromRegular = false;

				if ( Message.CurSize > MaxReliablePayload )
				{
					CreateFragmentBuffers( Message );

					Message.CurSize = 0;
				}
			}

			for ( var i = 0; i < MaxStreams; i++ )
			{
				_fragStartPos[i] = 0;
				_reliableFragment[i] = false;
				_reliableFragId[i] = 0;
				_fragLength[i] = 0;

				if ( _fragBufs[i] is not null )
					sendFromFragment = true;
			}

			if ( sendFromRegular || sendFromFragment )
			{
				ReliableSequence ^= 1;
				sendReliable = true;
			}

			if ( sendFromRegular )
			{
				Array.Copy( Message.Data, _reliableBuffer, Message.CurSize );

				_reliableLength = Message.CurSize;

				Message.CurSize = 0;

				for ( var i = 0; i < MaxStreams; i++ )
					_fragStartPos[i] = _reliableLength;
			}

			for ( var i = 0; i < MaxStreams; i++ )
			{
				if ( _fragBufs[i] is not { } buffers || !buffers.TryPeek( out var buffer ) || _reliableLength + buffer.Data.Length >= MaxReliablePayload )
					continue;

				_reliableFragId[i] = (buffer.BufferId << 16) | (_fragBufCount[i] & 0xFFFF);

				Array.Copy( buffer.Data, 0, _reliableBuffer, _reliableLength, buffer.Data.Length );

				_reliableLength += buffer.Data.Length;
				_fragLength[i] = buffer.Data.Length;

				buffers.Dequeue();

				if ( buffers.Count == 0 )
					_fragBufs[i] = null;

				_reliableFragment[i] = true;

				for ( var j = i + 1; j < MaxStreams; j++ )
					_fragStartPos[j] += _fragLength[i];
			}
		}

		var sequence = (uint)OutgoingSequence | (sendReliable ? ReliableBit : 0u);
		var acknowledge = (uint)IncomingSequence | ((uint)IncomingReliableSequence << 31);
		var sendFragments = _reliableFragment[0] || _reliableFragment[1];

		if ( sendReliable && sendFragments )
			sequence |= FragmentBit;

		OutgoingSequence++;

		send.WriteLong( unchecked((int)sequence) );
		send.WriteLong( unchecked((int)acknowledge) );

		if ( sendReliable )
		{
			if ( sendFragments )
			{
				for ( var i = 0; i < MaxStreams; i++ )
				{
					if ( !_reliableFragment[i] )
					{
						send.WriteByte( 0 );
						continue;
					}

					send.WriteByte( 1 );
					send.WriteLong( _reliableFragId[i] );
					send.WriteShort( _fragStartPos[i] );
					send.WriteShort( _fragLength[i] );
				}
			}

			send.Write( _reliableBuffer, _reliableLength );

			LastReliableSequence = OutgoingSequence - 1;
		}

		var length = data?.CurSize ?? 0;

		if ( (resending ? MaxRoutablePacket : send.MaxSize) - send.CurSize < length )
			Log.Trace( "Netchan_Transmit:  Unreliable would overfow, ignoring\n" );
		else if ( data is not null )
			send.Write( data );

		while ( send.CurSize < MinPacketSize )
			send.WriteByte( 1 );

		AddFlow( FlowOutgoing, send.CurSize );

		var packet = send.Data.AsSpan( 0, send.CurSize ).ToArray();

		Common.Munge2( packet.AsSpan( PacketHeaderSize ), (OutgoingSequence - 1) & 0xFF );

		Send?.Invoke( packet );

		var rate = Server.Current is { Active: true } && Cvar.GetFloat( "sv_lan" ) != 0f && LanRate > 1000f ? 1f / LanRate : (float)(1.0 / Rate);

		if ( ClearTime < Host.RealTime )
			ClearTime = Host.RealTime;

		ClearTime = ((send.CurSize + UdpHeaderSize) * rate) + ClearTime;
	}

	private static int FragmentId( FragBuf buffer ) => (buffer.BufferId >> 16) & 0xFFFF;

	private static FragBuf FindBuffer( List<FragBuf> list, int id )
	{
		foreach ( var buffer in list )
		{
			if ( buffer.BufferId == id )
				return buffer;
		}

		var created = new FragBuf { BufferId = id };

		for ( var i = 1; i < list.Count; i++ )
		{
			if ( FragmentId( list[i] ) <= FragmentId( created ) )
				continue;

			list[i] = created;

			return created;
		}

		list.Add( created );

		return created;
	}

	private void CheckForCompletion( int stream, int total )
	{
		var count = 0;

		foreach ( var buffer in _incomingBufs[stream] )
		{
			count++;

			if ( FragmentId( buffer ) == count || !_client )
				continue;

			Log.Info( "Netchan_CheckForCompletion:  Lost/dropped fragment would cause stall, retrying connection\n" );

			Cmd.AddText( "retry\n" );
		}

		if ( count != 0 && count == total )
			_incomingReady[stream] = true;
	}

	public bool Process( byte[] packet, MessageReader message )
	{
		LastReceived = (float)Host.RealTime;

		var size = packet.Length;
		var sequence = BitConverter.ToUInt32( packet, 0 );
		var acknowledge = BitConverter.ToUInt32( packet, 4 );

		if ( (acknowledge & FragmentBit) != 0 )
			return false;

		Common.UnMunge2( packet.AsSpan( PacketHeaderSize ), (int)(sequence & 0xFF) );

		message.BeginReading( packet, size );
		message.ReadCount = PacketHeaderSize;

		var reliableMessage = (sequence & ReliableBit) != 0;
		var reliableAcknowledge = (int)(acknowledge >> 31);
		var containsFragments = (sequence & FragmentBit) != 0;

		Span<bool> fragment = stackalloc bool[MaxStreams];
		Span<int> fragmentId = stackalloc int[MaxStreams];
		Span<int> fragmentOffset = stackalloc int[MaxStreams];
		Span<int> fragmentLength = stackalloc int[MaxStreams];

		if ( containsFragments )
		{
			for ( var i = 0; i < MaxStreams; i++ )
			{
				if ( message.ReadByte() == 0 )
					continue;

				fragment[i] = true;
				fragmentId[i] = message.ReadLong();
				fragmentOffset[i] = message.ReadShort();
				fragmentLength[i] = message.ReadShort();
			}

			for ( var i = 0; i < MaxStreams; i++ )
			{
				if ( !fragment[i] )
					continue;

				if ( ((fragmentId[i] >> 16) & 0xFFFF) > MaxFragmentId || (fragmentId[i] & 0xFFFF) > MaxFragmentId || (uint)fragmentLength[i] > MaxFragmentLength || (uint)fragmentOffset[i] > MaxFragmentOffset )
					return false;
			}
		}

		var incoming = (int)(sequence & ~(ReliableBit | FragmentBit));

		if ( incoming <= IncomingSequence )
			return false;

		Dropped = incoming - (IncomingSequence + 1);

		if ( reliableAcknowledge == ReliableSequence && IncomingAcknowledged + 1 >= LastReliableSequence )
			_reliableLength = 0;

		IncomingSequence = incoming;
		IncomingAcknowledged = (int)(acknowledge & ~(ReliableBit | FragmentBit));
		IncomingReliableAcknowledged = reliableAcknowledge;

		if ( reliableMessage )
			IncomingReliableSequence ^= 1;

		AddFlow( FlowIncoming, size );

		if ( !containsFragments )
			return true;

		for ( var i = 0; i < MaxStreams; i++ )
		{
			if ( !fragment[i] )
				continue;

			var write = message.ReadCount + fragmentOffset[i];
			var read = write + fragmentLength[i];

			if ( read > size )
				return false;

			if ( fragmentId[i] != 0 )
			{
				if ( fragmentLength[i] > FragmentBufferSize )
				{
					if ( !_client )
						Message.SetOverflowed();

					return false;
				}

				FindBuffer( _incomingBufs[i], fragmentId[i] ).Data = packet.AsSpan( write, fragmentLength[i] ).ToArray();

				CheckForCompletion( i, fragmentId[i] & 0xFFFF );
			}

			Array.Copy( packet, read, packet, write, size - read );

			size -= fragmentLength[i];

			for ( var j = i + 1; j < MaxStreams; j++ )
				fragmentOffset[j] -= fragmentLength[i];
		}

		var position = message.ReadCount;

		message.BeginReading( packet, size );
		message.ReadCount = position;

		return size > MinPacketSize;
	}

	public bool IncomingReady => _incomingReady[0] || _incomingReady[1];

	public byte[] CopyNormalFragments()
	{
		if ( !_incomingReady[NormalStream] )
			return null;

		var list = _incomingBufs[NormalStream];

		if ( list.Count == 0 )
		{
			Log.Info( "Netchan_CopyNormalFragments:  Called with no fragments readied\n" );

			_incomingReady[NormalStream] = false;

			return null;
		}

		var total = 0;

		foreach ( var buffer in list )
			total += buffer.Data.Length;

		var data = total > NetMessageSize ? null : new byte[total];
		var position = 0;

		foreach ( var buffer in list )
		{
			if ( data is null )
				break;

			Array.Copy( buffer.Data, 0, data, position, buffer.Data.Length );

			position += buffer.Data.Length;
		}

		list.Clear();

		_incomingReady[NormalStream] = false;

		return data;
	}
}
