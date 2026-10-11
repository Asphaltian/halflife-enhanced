using System;
using System.Globalization;

namespace GoldSrc;

public struct NetResponse
{
	public int Error;
	public int Context;
	public int Type;
	public string RemoteAddress;
	public double Ping;
	public object Response;
}

public struct NetStatus
{
	public bool Connected;
	public string LocalAddress;
	public string RemoteAddress;
	public int PacketLoss;
	public double Latency;
	public double ConnectionTime;
	public double Rate;
}

partial class Client
{
	public const int NetRequestServerList = 0;
	public const int NetRequestPing = 1;
	public const int NetRequestRules = 2;
	public const int NetRequestPlayers = 3;
	public const int NetRequestDetails = 4;

	public const int NetMultipleResponse = 1 << 0;

	public const int NetSuccess = 0;
	public const int NetErrorTimeout = 1 << 0;
	public const int NetErrorProtoUnsupported = 1 << 1;
	public const int NetErrorUndefined = 1 << 2;

	private sealed class NetRequest
	{
		public int Context;
		public int Type;
		public int Flags;
		public double SendTime;
		public double Timeout;
		public string RemoteAddress;
		public Action<NetResponse> Response;
	}

	private List<NetRequest> _netRequests = [];
	private double _latency;

	public void NetInitNetworking()
	{
	}

	public NetStatus NetGetStatus()
	{
		var connected = _state != ClientState.Disconnected;

		var status = new NetStatus
		{
			Connected = connected,
			LocalAddress = "",
			RemoteAddress = _netchan.Address,
			Rate = Common.Atof( Rate )
		};

		if ( !connected )
			return status;

		status.PacketLoss = _state == ClientState.Active ? Math.Clamp( (int)_packetLoss, 0, 100 ) : 0;
		status.Latency = _latency;
		status.ConnectionTime = Host.RealTime - _netchan.ConnectTime;

		return status;
	}

	public void NetSendRequest( int context, int request, int flags, double timeout, string remoteAddress, Action<NetResponse> response )
	{
		_netRequests.Insert( 0, new NetRequest
		{
			Context = context,
			Type = request,
			Flags = flags,
			SendTime = Host.RealTime,
			Timeout = timeout,
			RemoteAddress = remoteAddress,
			Response = response
		} );

		if ( request is < NetRequestServerList or > NetRequestDetails )
			Log.Info( $"Unknown request type:  {request}\n" );
	}

	public void NetCancelRequest( int context )
	{
		var index = _netRequests.FindIndex( x => x.Context == context );

		if ( index < 0 )
			return;

		_netRequests.RemoveAt( index );
		_netRequests.Reverse();
	}

	public void NetCancelAllRequests() => _netRequests.Clear();

	private void NetCheckTimeouts()
	{
		if ( _netRequests.Count == 0 )
			return;

		var requests = _netRequests;
		var kept = new List<NetRequest>( requests.Count );

		_netRequests = kept;

		foreach ( var request in requests )
		{
			if ( Host.RealTime <= request.SendTime + request.Timeout )
			{
				kept.Insert( 0, request );
				continue;
			}

			request.Response?.Invoke( new NetResponse
			{
				Error = NetErrorTimeout,
				Context = request.Context,
				Type = request.Type,
				RemoteAddress = request.RemoteAddress,
				Ping = Host.RealTime - request.SendTime
			} );
		}
	}

	public static string NetBroadcastAddress( int port ) => $"255.255.255.255:{port}";

	public static string NetBroadcastIpxAddress( int port ) => $"00000000:ffffffffffff:{port}";

	public static string NetAdrToString( string address ) => address ?? "";

	public static bool NetCompareAdr( string a, string b ) => string.Equals( a, b, StringComparison.Ordinal );

	public static bool NetStringToAdr( string text, out string address )
	{
		address = null;

		if ( string.IsNullOrEmpty( text ) )
			return false;

		var colon = text.IndexOf( ':' );
		var host = colon >= 0 ? text[..colon] : text;
		var port = colon >= 0 ? Common.Atoi( text[(colon + 1)..] ) : 0;
		var parts = host.Split( '.' );

		if ( parts.Length != 4 )
			return false;

		Span<int> bytes = stackalloc int[4];

		for ( var i = 0; i < 4; i++ )
		{
			if ( !int.TryParse( parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out bytes[i] ) || bytes[i] > 255 )
				return false;
		}

		address = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}:{port}";

		return true;
	}
}
