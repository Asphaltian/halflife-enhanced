using System;
using GoldSrc;

namespace HalfLife;

partial class HalfLifeDll
{
	private const int SayTextLength = 128;

	private static bool IsValidUChar32( uint value )
	{
		return value < 0x110000u && (value - 0x00D800u) > 0x7FFu && (value & 0xFFFFu) < 0xFFFEu && (value - 0x00FDD0u) > 0x1Fu;
	}

	private static int Utf8ToUChar32( string text, int position, out bool error )
	{
		uint At( int index ) => position + index < text.Length ? text[position + index] : 0u;

		var value = At( 0 );

		error = false;

		if ( value < 0x80 )
			return 1;

		error = true;

		if ( (value - 0xC0u) > 0x37u || (At( 1 ) & 0xC0) != 0x80 )
			return 1;

		value = (value << 6) - (0xC0 << 6) + At( 1 ) - 0x80;

		var bytes = 2;
		var minimum = 0x80u;

		if ( (value & (0x20 << 6)) != 0 )
		{
			if ( (At( 2 ) & 0xC0) != 0x80 )
				return 2;

			value = (value << 6) - (0x20 << 12) + At( 2 ) - 0x80;
			bytes = 3;
			minimum = 0x800;

			if ( (value & (0x10 << 12)) != 0 )
			{
				if ( (At( 3 ) & 0xC0) != 0x80 )
					return 3;

				value = (value << 6) - (0x10 << 18) + At( 3 ) - 0x80;
				bytes = 4;
				minimum = 0x10000;
			}
			else if ( (value - 0xD800u) < 0x400u && At( 3 ) == 0xED && (byte)(At( 4 ) - 0xB0) < 0x10 && (At( 5 ) & 0xC0) == 0x80 )
			{
				value = 0x10000 + ((value - 0xD800u) << 10) + ((uint)(byte)(At( 4 ) - 0xB0) << 6) + At( 5 ) - 0x80;
				bytes = 6;
				minimum = 0x10000;
			}
		}

		error = value < minimum || !IsValidUChar32( value );

		return bytes;
	}

	private static bool UnicodeValidate( string text )
	{
		for ( var position = 0; position < text.Length; )
		{
			var size = Utf8ToUChar32( text, position, out var error );

			if ( error || size == 6 )
				return false;

			position += size;
		}

		return true;
	}

	private static void HostSay( Player player, bool teamOnly )
	{
		if ( Cmd.Argc == 0 )
			return;

		var server = player.Server;

		if ( player.NextChatTime > server.Globals.Time )
			return;

		var command = Cmd.Argv( 0 );
		string text;

		if ( command.Equals( "say", StringComparison.OrdinalIgnoreCase ) || command.Equals( "say_team", StringComparison.OrdinalIgnoreCase ) )
		{
			if ( Cmd.Argc < 2 )
				return;

			text = Cmd.Args;
		}
		else
		{
			text = Cmd.Argc >= 2 ? $"{command} {Cmd.Args}" : command;
		}

		if ( text.StartsWith( '"' ) )
		{
			text = text[1..];

			if ( text.Length > 0 )
				text = text[..^1];
		}

		if ( text.Length == 0 || !UnicodeValidate( text ) )
			return;

		string message;

		if ( player.IsObserver && teamOnly )
			message = $"\u0002(SPEC) {player.NetName}: ";
		else if ( teamOnly )
			message = $"\u0002(TEAM) {player.NetName}: ";
		else
			message = $"\u0002{player.NetName}: ";

		var room = SayTextLength - 2 - message.Length;

		if ( text.Length > room )
			text = text[..room];

		message = $"{message}{text}\n";

		player.NextChatTime = (float)(server.Globals.Time + Player.ChatInterval);

		BaseEntity client = null;

		while ( (client = Util.FindEntityByClassname( client, "player" )) is not null && !Util.IsNullEnt( client ) )
		{
			if ( client == player || !client.IsNetClient )
				continue;

			if ( !player.IsObserver && teamOnly && GameRules.PlayerRelationship( client, player ) != HalfLife.PlayerRelationship.Teammate )
				continue;

			if ( player.IsObserver && teamOnly && client is Player { IsObserver: false } )
				continue;

			server.MessageBegin( MessageDest.One, UserMessages.SayText, default, client );
			server.WriteByte( player.Index );
			server.WriteString( message );
			server.MessageEnd();
		}

		server.MessageBegin( MessageDest.One, UserMessages.SayText, default, player );
		server.WriteByte( player.Index );
		server.WriteString( message );
		server.MessageEnd();

		Log.Info( message );
	}
}
