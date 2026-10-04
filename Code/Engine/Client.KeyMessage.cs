using System.Text;

namespace GoldSrc;

partial class Client
{
	private const int MaxChatBuffer = 119;
	private const int MaxMessageType = 31;
	private const string DefaultMessageType = "say";

	private string _chatBuffer = "";
	private string _messageType = DefaultMessageType;

	public bool MessageMode { get; private set; }

	[ConCmd( "messagemode" )]
	private static void MessageModeCommand( string type = null )
	{
		if ( ClientDll.Loaded?.Engine is not { } client )
			return;

		client.MessageMode = true;

		if ( type is null )
			client._messageType = DefaultMessageType;
		else
			client._messageType = type.Length > MaxMessageType ? type[..MaxMessageType] : type;
	}

	[ConCmd( "messagemode2" )]
	private static void MessageMode2Command()
	{
		if ( ClientDll.Loaded?.Engine is not { } client )
			return;

		client.MessageMode = true;
		client._messageType = "say_team";
	}

	public void KeyMessage( string key )
	{
		switch ( key )
		{
			case "enter":
			case "pad_enter":
				Cmd.AddText( $"{_messageType} \"{_chatBuffer.Replace( ';', ',' )}\"\n" );
				break;

			case "escape":
				break;

			case "backspace":
				if ( _chatBuffer.Length > 0 )
				{
					var last = char.IsLowSurrogate( _chatBuffer[^1] ) && _chatBuffer.Length > 1 ? 2 : 1;

					_chatBuffer = _chatBuffer[..^last];
				}

				return;

			default:
				return;
		}

		MessageMode = false;
		_chatBuffer = "";
		_messageType = DefaultMessageType;
	}

	public void KeyMessageText( string text )
	{
		if ( !MessageMode )
			return;

		var length = Encoding.UTF8.GetByteCount( _chatBuffer );

		foreach ( var rune in text.EnumerateRunes() )
		{
			length += rune.Utf8SequenceLength;

			if ( length > MaxChatBuffer )
				break;

			_chatBuffer += rune.ToString();
		}
	}

	private void DrawMessageMode()
	{
		if ( !MessageMode )
			return;

		ResetTextColor();

		var skip = _chatBuffer.Length - (ScreenWidth / 10);
		var text = skip > 0 ? _chatBuffer[skip..] : _chatBuffer;

		var x = 8;
		var y = 0;

		_clientDll.ChatInputPosition( ref x, ref y );

		x = DrawConsoleString( x, y, _messageType );
		x = DrawConsoleString( x + 3, y, ":" );

		DrawConsoleString( x + 3, y, text );
	}
}
