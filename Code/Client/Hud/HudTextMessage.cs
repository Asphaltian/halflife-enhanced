using System.Text;

namespace HalfLife;

public sealed class HudTextMessage( Hud hud ) : HudBase( hud )
{
	public const int HudPrintNotify = 1;
	public const int HudPrintConsole = 2;
	public const int HudPrintTalk = 3;
	public const int HudPrintCenter = 4;

	private const int MessageBufferSize = 128;
	private const int LocaliseBufferSize = 1024;

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "TextMsg", MsgFuncTextMsg );

		Hud.AddHudElem( this );

		Reset();
	}

	public string LocaliseTextString( string message, int bufferSize )
	{
		var builder = new StringBuilder();
		var source = 0;

		for ( var remaining = bufferSize; source < message.Length && remaining > 0; remaining-- )
		{
			if ( message[source] != '#' )
			{
				builder.Append( message[source++] );
				continue;
			}

			var wordStart = source;

			source++;

			while ( source < message.Length && message[source] is (>= 'A' and <= 'z') or (>= '0' and <= '9') )
				source++;

			var client = Engine.TextMessageGet( message[(wordStart + 1)..source] );

			if ( client?.Message is null )
			{
				source = wordStart;
				builder.Append( message[source++] );
				continue;
			}

			builder.Append( client.Message );
		}

		return builder.Length >= bufferSize ? builder.ToString( 0, bufferSize - 1 ) : builder.ToString();
	}

	public string BufferedLocaliseTextString( string message ) => LocaliseTextString( message, LocaliseBufferSize );

	private string LookupString( string message, ref int destination, bool overrideDestination )
	{
		if ( message.Length == 0 || message[0] != '#' )
			return message;

		var client = Engine.TextMessageGet( message[1..] );

		if ( client?.Message is null )
			return message;

		if ( overrideDestination && client.Effect < 0 )
			destination = -client.Effect;

		return client.Message;
	}

	private static string SafeCopy( string text ) => text.Length >= MessageBufferSize ? text[..(MessageBufferSize - 1)] : text;

	private static string StripEndNewline( string text )
	{
		return text.Length > 0 && text[^1] is '\n' or '\r' ? text[..^1] : text;
	}

	private static string ConvertCrToNl( string text ) => text.Replace( '\r', '\n' );

	private static string Format( string format, int size, params string[] arguments )
	{
		var builder = new StringBuilder();
		var argument = 0;

		for ( var i = 0; i < format.Length; i++ )
		{
			if ( format[i] != '%' || i + 1 >= format.Length )
			{
				builder.Append( format[i] );
				continue;
			}

			i++;

			if ( format[i] == 's' && argument < arguments.Length )
				builder.Append( arguments[argument++] );
			else if ( format[i] == '%' )
				builder.Append( '%' );
		}

		return builder.Length >= size ? builder.ToString( 0, size - 1 ) : builder.ToString();
	}

	public void TextMsg( int destination, string text )
	{
		var data = new byte[text.Length + 2];

		data[0] = (byte)destination;

		for ( var i = 0; i < text.Length; i++ )
			data[i + 1] = (byte)text[i];

		MsgFuncTextMsg( null, data );
	}

	private bool MsgFuncTextMsg( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var destination = message.ReadByte();
		var unused = 0;

		var text = SafeCopy( LookupString( message.ReadString(), ref destination, true ) );
		var string1 = StripEndNewline( SafeCopy( LookupString( message.ReadString(), ref unused, false ) ) );
		var string2 = StripEndNewline( SafeCopy( LookupString( message.ReadString(), ref unused, false ) ) );
		var string3 = StripEndNewline( SafeCopy( LookupString( message.ReadString(), ref unused, false ) ) );
		var string4 = StripEndNewline( SafeCopy( LookupString( message.ReadString(), ref unused, false ) ) );

		if ( Hud.ViewPort is { } viewPort && !viewPort.AllowedToPrintText() )
			return true;

		switch ( destination )
		{
			case HudPrintCenter:
				Engine.CenterPrint( ConvertCrToNl( Format( text, MessageBufferSize, string1, string2, string3, string4 ) ) );
				break;

			case HudPrintNotify:
				Engine.ConsolePrint( ConvertCrToNl( $"\u0001{Format( text, MessageBufferSize - 1, string1, string2, string3, string4 )}" ) );
				break;

			case HudPrintTalk:
				Hud.SayText.SayTextPrint( ConvertCrToNl( Format( text, MessageBufferSize, string1, string2, string3, string4 ) ), 128 );
				break;

			case HudPrintConsole:
				Engine.ConsolePrint( ConvertCrToNl( Format( text, MessageBufferSize, string1, string2, string3, string4 ) ) );
				break;
		}

		return true;
	}
}
