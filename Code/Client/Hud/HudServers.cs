using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudServers( Client engine )
{
	private sealed class Request
	{
		public Request Next;
		public string RemoteAddress;
		public int Context;
	}

	private sealed class ServerEntry
	{
		public ServerEntry Next;
		public string RemoteAddress;
		public string Info;
		public int Ping;
	}

	private const string ValveMasterAddress = "half-life.east.won.net";
	private const int PortMaster = 27010;
	private const int PortServer = 27015;
	private const string MasterParseFile = "valvecomm.lst";
	private const int MaxQueries = 20;

	private static int _contextId;

	private int _requesting;
	private int _done;
	private Request _serverList;
	private Request _activeList;
	private ServerEntry _servers;
	private int _serverCount;
	private int _activeQueries;
	private int _querying;
	private double _elapsed;

	public void ListResponse( NetResponse response )
	{
		if ( response.Error != Client.NetSuccess )
			return;

		if ( response.Type != Client.NetRequestServerList )
			return;

		if ( response.Response is string[] list )
		{
			foreach ( var address in list )
				_serverList = new Request { Context = -1, RemoteAddress = address, Next = _serverList };
		}

		Con.Printf( "got list\n" );

		_querying = 1;
		_activeQueries = 0;
	}

	public void ServerResponse( NetResponse response )
	{
		var request = FindRequest( response.Context, _activeList );

		if ( request is not null )
		{
			RemoveServerFromList( ref _activeList, request );

			_activeQueries--;
		}

		if ( response.Error != Client.NetSuccess )
			return;

		if ( response.Type != Client.NetRequestDetails || response.Response is not string text )
			return;

		var length = text.Length + 100 + 1;
		var ping = (int)(1000.0 * response.Ping);
		var browser = new ServerEntry { RemoteAddress = response.RemoteAddress, Ping = ping, Info = text };

		Info.SetValueForKey( ref browser.Info, "address", Client.NetAdrToString( response.RemoteAddress ), length );
		Info.SetValueForKey( ref browser.Info, "ping", ping.ToString(), length );

		AddServer( ref _servers, browser );
	}

	public void PingResponse( NetResponse response )
	{
		if ( response.Error != Client.NetSuccess )
			return;

		if ( response.Type == Client.NetRequestPing )
			Con.Printf( $"ping == {1000.0 * response.Ping:F2}\n" );
	}

	public void RulesResponse( NetResponse response )
	{
		if ( response.Error != Client.NetSuccess )
			return;

		if ( response.Type == Client.NetRequestRules && response.Response is string text )
			Con.Printf( $"rules {text}\n" );
	}

	public void PlayersResponse( NetResponse response )
	{
		if ( response.Error != Client.NetSuccess )
			return;

		if ( response.Type == Client.NetRequestPlayers && response.Response is string text )
			Con.Printf( $"players {text}\n" );
	}

	private static bool CompareServers( ServerEntry p1, ServerEntry p2 )
	{
		if ( p1.Ping < p2.Ping )
			return true;

		if ( p1.Ping == p2.Ping && p1.Info is not null && p2.Info is not null )
		{
			var n1 = Info.ValueForKey( p1.Info, "hostname" );
			var n2 = Info.ValueForKey( p2.Info, "hostname" );

			if ( n1 is not null && n2 is not null && string.Compare( n1, n2, StringComparison.OrdinalIgnoreCase ) < 0 )
				return true;
		}

		return false;
	}

	private void AddServer( ref ServerEntry list, ServerEntry p )
	{
		if ( p is null )
			return;

		_serverCount++;

		var current = list;

		if ( current is null )
		{
			p.Next = null;
			list = p;

			return;
		}

		if ( CompareServers( p, current ) )
		{
			p.Next = list;
			list = p;

			return;
		}

		while ( current.Next is not null )
		{
			if ( CompareServers( p, current.Next ) )
			{
				p.Next = current.Next.Next;
				current.Next = p;

				return;
			}

			current = current.Next;
		}

		p.Next = null;
		current.Next = p;
	}

	public void Think( double time )
	{
		_elapsed += time;

		if ( _requesting == 0 )
			return;

		if ( _querying == 0 )
			return;

		QueryThink();

		if ( ServerListSize() > 0 )
			return;

		_requesting = 0;
		_done = 0;
		_querying = 0;
		_activeQueries = 0;
	}

	public void QueryThink()
	{
		if ( _requesting == 0 || _done != 0 )
			return;

		if ( _querying == 0 )
			return;

		if ( _activeQueries > MaxQueries )
			return;

		if ( _serverList is null )
			return;

		while ( true )
		{
			var p = _serverList;

			if ( p is null )
				break;

			_serverList = _serverList.Next;

			p.Context = _contextId;

			engine.NetSendRequest( _contextId++, Client.NetRequestDetails, 0, 2.0, p.RemoteAddress, ServerResponse );

			_activeQueries++;

			p.Next = _activeList;
			_activeList = p;

			if ( _activeQueries > MaxQueries )
				break;
		}
	}

	public int ServerListSize()
	{
		var count = 0;

		for ( var p = _serverList; p is not null; p = p.Next )
			count++;

		for ( var p = _activeList; p is not null; p = p.Next )
			count++;

		return count;
	}

	private static Request FindRequest( int context, Request list )
	{
		for ( var p = list; p is not null; p = p.Next )
		{
			if ( context == p.Context )
				return p;
		}

		return null;
	}

	private static void RemoveServerFromList( ref Request list, Request item )
	{
		Request newList = null;
		var p = list;

		while ( p is not null )
		{
			var next = p.Next;

			if ( p != item )
			{
				p.Next = newList;
				newList = p;
			}

			p = next;
		}

		list = newList;
	}

	private static int CompareField( ServerEntry p1, ServerEntry p2, string fieldName, int sortOrder )
	{
		var sz1 = Info.ValueForKey( p1.Info, fieldName );
		var sz2 = Info.ValueForKey( p2.Info, fieldName );
		var fv1 = Common.Atof( sz1 );
		var fv2 = Common.Atof( sz2 );

		if ( fv1 != 0f && fv2 != 0f )
		{
			if ( fv1 > fv2 )
				return sortOrder;

			if ( fv1 < fv2 )
				return -sortOrder;

			return 0;
		}

		return string.Compare( sz1, sz2, StringComparison.OrdinalIgnoreCase );
	}

	public void SortServers( string fieldName )
	{
		if ( _servers is null )
			return;

		var sorted = new List<ServerEntry>();

		for ( var p = _servers; p is not null; p = p.Next )
			sorted.Add( p );

		sorted.Sort( ( a, b ) => CompareField( a, b, fieldName, 1 ) );

		_servers = sorted[0];

		for ( var i = 0; i < sorted.Count - 1; i++ )
			sorted[i].Next = sorted[i + 1];

		sorted[^1].Next = null;
	}

	private ServerEntry GetServer( int server )
	{
		var count = 0;

		for ( var p = _servers; p is not null; p = p.Next )
		{
			if ( count == server )
				return p;

			count++;
		}

		return null;
	}

	public string GetServerInfo( int server ) => GetServer( server )?.Info;

	public void CancelRequest()
	{
		_requesting = 0;
		_querying = 0;
		_done = 1;

		engine.NetCancelAllRequests();
	}

	private bool LoadMasterAddresses( int maxServers, out int count, string[] addresses )
	{
		var found = 0;
		var defaultPort = 0;

		if ( Server.LoadMountText( MasterParseFile ) is { } file )
		{
			var position = 0;

			while ( found < maxServers )
			{
				var token = Common.Parse( file, ref position ) ?? "";

				if ( token.Length <= 0 )
					break;

				var ignore = true;

				if ( string.Equals( token, "Master", StringComparison.OrdinalIgnoreCase ) )
				{
					defaultPort = PortMaster;
					ignore = false;
				}

				token = Common.Parse( file, ref position ) ?? "";

				if ( token.Length <= 0 )
					break;

				if ( !string.Equals( token, "{", StringComparison.OrdinalIgnoreCase ) )
					break;

				while ( found < maxServers )
				{
					token = Common.Parse( file, ref position ) ?? "";

					if ( token.Length <= 0 )
						break;

					if ( string.Equals( token, "}", StringComparison.OrdinalIgnoreCase ) )
						break;

					var baseAddress = token;

					token = Common.Parse( file, ref position ) ?? "";

					if ( token.Length <= 0 )
						break;

					if ( !string.Equals( token, ":", StringComparison.OrdinalIgnoreCase ) )
						break;

					token = Common.Parse( file, ref position ) ?? "";

					if ( token.Length <= 0 )
						break;

					var port = Common.Atoi( token );

					if ( port == 0 )
						port = defaultPort;

					if ( !Client.NetStringToAdr( $"{baseAddress}:{port}", out var address ) )
						ignore = true;

					if ( !ignore )
						addresses[found++] = address;
				}
			}
		}

		if ( found == 0 && Client.NetStringToAdr( ValveMasterAddress, out var master ) )
			addresses[found++] = master;

		count = found;

		return found > 0;
	}

	public void RequestList()
	{
		_requesting = 1;
		_done = 0;

		var addresses = new string[1];

		if ( !LoadMasterAddresses( 1, out _, addresses ) )
		{
			Con.DPrintf( "SendRequest:  Unable to read master server addresses\n" );
			return;
		}

		_activeList = null;
		_serverList = null;
		_servers = null;
		_serverCount = 0;

		engine.NetInitNetworking();
		engine.NetCancelAllRequests();
		engine.NetSendRequest( _contextId++, Client.NetRequestServerList, 0, 5.0, addresses[0], ListResponse );
	}

	public void RequestBroadcastList( int clearPending )
	{
		_requesting = 1;
		_done = 0;

		if ( clearPending != 0 )
		{
			_activeList = null;
			_serverList = null;
			_servers = null;
			_serverCount = 0;
		}

		engine.NetInitNetworking();

		if ( clearPending != 0 )
			engine.NetCancelAllRequests();

		engine.NetSendRequest( _contextId++, Client.NetRequestDetails, Client.NetMultipleResponse, 5.0, Client.NetBroadcastAddress( PortServer ), ServerResponse );
		engine.NetSendRequest( _contextId++, Client.NetRequestDetails, Client.NetMultipleResponse, 5.0, Client.NetBroadcastIpxAddress( PortServer ), ServerResponse );
	}

	public void ServerPing( int server )
	{
		if ( GetServer( server ) is not { } p )
			return;

		engine.NetInitNetworking();
		engine.NetSendRequest( _contextId++, Client.NetRequestPing, 0, 5.0, p.RemoteAddress, PingResponse );
	}

	public void ServerRules( int server )
	{
		if ( GetServer( server ) is not { } p )
			return;

		engine.NetInitNetworking();
		engine.NetSendRequest( _contextId++, Client.NetRequestRules, 0, 5.0, p.RemoteAddress, RulesResponse );
	}

	public void ServerPlayers( int server )
	{
		if ( GetServer( server ) is not { } p )
			return;

		engine.NetInitNetworking();
		engine.NetSendRequest( _contextId++, Client.NetRequestPlayers, 0, 5.0, p.RemoteAddress, PlayersResponse );
	}

	public bool IsQuerying() => _requesting != 0;

	public int GetServerCount() => _serverCount;
}
