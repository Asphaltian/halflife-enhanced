using System;
using GoldSrc;

namespace HalfLife;

public sealed class VoiceBanMgr
{
	public const int PlayerIdLength = 16;

	private const int FileVersion = 1;
	private const string FileName = "voice_ban.dt";
	private const int HashSize = 256;

	private readonly List<byte[]>[] _playerHash = new List<byte[]>[HashSize];

	public VoiceBanMgr()
	{
		for ( var i = 0; i < HashSize; i++ )
			_playerHash[i] = [];
	}

	private static byte HashPlayerId( byte[] playerId )
	{
		byte hash = 0;

		for ( var i = 0; i < PlayerIdLength; i++ )
			hash += playerId[i];

		return hash;
	}

	public bool Init( string gameDir )
	{
		Term();

		var file = Server.ReadRootFile( $"{gameDir}/{FileName}" );

		if ( file is not null && file.Length >= sizeof( int ) && BitConverter.ToInt32( file, 0 ) == FileVersion )
		{
			var count = (file.Length - sizeof( int )) / PlayerIdLength;

			for ( var i = 0; i < count; i++ )
				AddBannedPlayer( file.AsSpan( sizeof( int ) + (i * PlayerIdLength), PlayerIdLength ).ToArray() );
		}

		return true;
	}

	public void Term()
	{
		for ( var i = 0; i < HashSize; i++ )
			_playerHash[i].Clear();
	}

	public void SaveState( string gameDir )
	{
		var data = new List<byte>();

		data.AddRange( BitConverter.GetBytes( FileVersion ) );

		for ( var i = 0; i < HashSize; i++ )
		{
			foreach ( var playerId in _playerHash[i] )
				data.AddRange( playerId );
		}

		Server.WriteRootFile( $"{gameDir}/{FileName}", [.. data] );
	}

	public bool GetPlayerBan( byte[] playerId ) => InternalFindPlayerSquelch( playerId ) is not null;

	public void SetPlayerBan( byte[] playerId, bool squelch )
	{
		if ( squelch )
		{
			if ( GetPlayerBan( playerId ) )
				return;

			AddBannedPlayer( playerId );
		}
		else
		{
			var player = InternalFindPlayerSquelch( playerId );

			if ( player is not null )
				_playerHash[HashPlayerId( playerId )].Remove( player );
		}
	}

	public void ForEachBannedPlayer( Action<byte[]> callback )
	{
		for ( var i = 0; i < HashSize; i++ )
		{
			foreach ( var playerId in _playerHash[i] )
				callback( playerId );
		}
	}

	private byte[] InternalFindPlayerSquelch( byte[] playerId )
	{
		foreach ( var current in _playerHash[HashPlayerId( playerId )] )
		{
			if ( playerId.AsSpan( 0, PlayerIdLength ).SequenceEqual( current ) )
				return current;
		}

		return null;
	}

	private byte[] AddBannedPlayer( byte[] playerId )
	{
		var added = playerId.AsSpan( 0, PlayerIdLength ).ToArray();

		_playerHash[HashPlayerId( playerId )].Add( added );

		return added;
	}
}
