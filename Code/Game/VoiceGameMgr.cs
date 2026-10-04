using System;
using GoldSrc;

namespace HalfLife;

public interface IVoiceGameMgrHelper
{
	bool CanPlayerHearPlayer( Player listener, Player talker );
}

public sealed class VoiceGameMgr
{
	private const int VoiceMaxPlayers = 32;
	private const int VoiceMaxPlayersDWords = 1;
	private const double UpdateInterval = 0.3;

	[ConVar( "voice_serverdebug" )]
	private static float VoiceServerDebugCvar { get; set; }

	[ConVar( "sv_alltalk" )]
	private static float AllTalk { get; set; }

	private readonly uint[] _banMasks = new uint[VoiceMaxPlayers];
	private readonly uint[] _sentGameRulesMasks = new uint[VoiceMaxPlayers];
	private readonly uint[] _sentBanMasks = new uint[VoiceMaxPlayers];

	private uint _playerModEnable;
	private uint _wantModEnable;
	private int _messagePlayerVoiceMask;
	private int _messageRequestState;
	private IVoiceGameMgrHelper _helper;
	private int _maxPlayers;
	private double _updateInterval;

	private static void VoiceServerDebug( string message )
	{
		if ( VoiceServerDebugCvar == 0f )
			return;

		Log.Info( message );
	}

	public bool Init( Server server, IVoiceGameMgrHelper helper, int maxClients )
	{
		_helper = helper;
		_maxPlayers = VoiceMaxPlayers < maxClients ? VoiceMaxPlayers : maxClients;

		server.PrecacheModel( "sprites/voiceicon.spr" );

		_messagePlayerVoiceMask = Server.RegUserMsg( "VoiceMask", VoiceMaxPlayersDWords * 4 * 2 );
		_messageRequestState = Server.RegUserMsg( "ReqState", 0 );

		return true;
	}

	public void Update( Server server, double frameTime )
	{
		_updateInterval += frameTime;

		if ( _updateInterval < UpdateInterval )
			return;

		UpdateMasks( server );
	}

	public void ClientConnected( int entityIndex )
	{
		var index = entityIndex - 1;

		_wantModEnable |= 1u << index;
		_sentGameRulesMasks[index] = 0;
		_sentBanMasks[index] = 0;
	}

	public bool PlayerHasBlockedPlayer( Player receiver, Player sender )
	{
		if ( receiver is null || sender is null )
			return false;

		var receiverIndex = receiver.Index - 1;
		var senderIndex = sender.Index - 1;

		if ( receiverIndex < 0 || receiverIndex >= _maxPlayers || senderIndex < 0 || senderIndex >= _maxPlayers )
			return false;

		return (_banMasks[receiverIndex] & (1u << senderIndex)) != 0;
	}

	public bool ClientCommand( Player player, string command )
	{
		var playerClientIndex = player.Index - 1;

		if ( playerClientIndex < 0 || playerClientIndex >= _maxPlayers )
		{
			VoiceServerDebug( $"CVoiceGameMgr::ClientCommand: cmd {command} from invalid client ({playerClientIndex})\n" );
			return true;
		}

		if ( command.Equals( "vban", StringComparison.OrdinalIgnoreCase ) && Cmd.Argc >= 2 )
		{
			for ( var i = 1; i < Cmd.Argc; i++ )
			{
				var mask = ScanHex( Cmd.Argv( i ) );

				if ( i <= VoiceMaxPlayersDWords )
				{
					VoiceServerDebug( $"CVoiceGameMgr::ClientCommand: vban (0x{mask:x}) from {playerClientIndex}\n" );

					_banMasks[playerClientIndex] = mask;
				}
				else
				{
					VoiceServerDebug( $"CVoiceGameMgr::ClientCommand: invalid index ({i})\n" );
				}
			}

			return true;
		}

		if ( command.Equals( "VModEnable", StringComparison.OrdinalIgnoreCase ) && Cmd.Argc >= 2 )
		{
			var enable = Util.Atoi( Cmd.Argv( 1 ) ) != 0;

			VoiceServerDebug( $"CVoiceGameMgr::ClientCommand: VModEnable ({(enable ? 1 : 0)})\n" );

			if ( enable )
				_playerModEnable |= 1u << playerClientIndex;
			else
				_playerModEnable &= ~(1u << playerClientIndex);

			_wantModEnable &= ~(1u << playerClientIndex);

			return true;
		}

		return false;
	}

	private static uint ScanHex( string text )
	{
		var position = 0;

		while ( position < text.Length && char.IsWhiteSpace( text[position] ) )
			position++;

		if ( position + 1 < text.Length && text[position] == '0' && text[position + 1] is 'x' or 'X' )
			position += 2;

		var value = 0u;

		for ( ; position < text.Length && Uri.IsHexDigit( text[position] ); position++ )
			value = (value << 4) | (uint)Convert.ToInt32( text[position].ToString(), 16 );

		return value;
	}

	private void UpdateMasks( Server server )
	{
		_updateInterval = 0;

		var allTalk = AllTalk != 0f;

		for ( var client = 0; client < _maxPlayers; client++ )
		{
			if ( Util.PlayerByIndex( client + 1 ) is not { IsPlayer: true } entity )
				continue;

			if ( (_wantModEnable & (1u << client)) != 0 )
			{
				server.MessageBegin( MessageDest.One, _messageRequestState, default, entity );
				server.MessageEnd();
			}

			var player = (Player)entity;
			var gameRulesMask = 0u;

			if ( (_playerModEnable & (1u << client)) != 0 )
			{
				for ( var otherClient = 0; otherClient < _maxPlayers; otherClient++ )
				{
					if ( Util.PlayerByIndex( otherClient + 1 ) is { } other && (allTalk || _helper.CanPlayerHearPlayer( player, (Player)other )) )
						gameRulesMask |= 1u << otherClient;
				}
			}

			if ( gameRulesMask != _sentGameRulesMasks[client] || _banMasks[client] != _sentBanMasks[client] )
			{
				_sentGameRulesMasks[client] = gameRulesMask;
				_sentBanMasks[client] = _banMasks[client];

				server.MessageBegin( MessageDest.One, _messagePlayerVoiceMask, default, player );
				server.WriteLong( (int)gameRulesMask );
				server.WriteLong( (int)_banMasks[client] );
				server.MessageEnd();
			}

			for ( var otherClient = 0; otherClient < _maxPlayers; otherClient++ )
			{
				var canHear = (gameRulesMask & (1u << otherClient)) != 0 && (_banMasks[client] & (1u << otherClient)) == 0;

				server.SetClientListening( client + 1, otherClient + 1, canHear );
			}
		}
	}
}
