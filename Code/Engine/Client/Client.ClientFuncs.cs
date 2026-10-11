namespace GoldSrc;

public abstract class GameClientExports
{
	public abstract string GetServerHostName();

	public abstract bool IsPlayerGameVoiceMuted( int playerIndex );

	public abstract void MutePlayerGameVoice( int playerIndex );

	public abstract void UnmutePlayerGameVoice( int playerIndex );
}

partial class Client
{
	private const int DatagramSize = 6000;

	private readonly SizeBuf _datagram = new( "cls.datagram", DatagramSize, false );

	public bool GetPlayerUniqueId( int player, byte[] playerId )
	{
		var slot = player - 1;

		if ( slot < 0 || slot >= _maxClients || _players[slot] is not { } info )
			return false;

		if ( info.UserInfo.Length == 0 || info.Name.Length == 0 )
			return false;

		info.HashedCdKey.CopyTo( playerId, 0 );

		return true;
	}

	public string PlayerInfoValueForKey( int player, string key )
	{
		if ( player > _maxClients || player <= 0 || _players[player - 1] is not { UserInfo.Length: > 0 } info )
			return null;

		return Info.ValueForKey( info.UserInfo, key );
	}
}
