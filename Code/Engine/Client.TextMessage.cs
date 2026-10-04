using System;
using System.Text;

namespace GoldSrc;

public sealed record ClientTextMessage
{
	public int Effect;
	public byte R1;
	public byte G1;
	public byte B1;
	public byte A1;
	public byte R2;
	public byte G2;
	public byte B2;
	public byte A2;
	public float X;
	public float Y;
	public float FadeIn;
	public float FadeOut;
	public float HoldTime;
	public float FxTime;
	public string Name;
	public string Message;
}

partial class Client
{
	private const int MaxMessages = 1000;
	private const int MessageNameHeapSize = 16384;
	private const int MessageLineLength = 512;
	private const int NetworkMessageLength = 512;
	private const string DemoMessageName = "__DEMOMESSAGE__";

	private static readonly string[] NetworkMessageNames = ["__NETMESSAGE__1", "__NETMESSAGE__2", "__NETMESSAGE__3", "__NETMESSAGE__4"];

	private readonly ClientTextMessage _messageParms = new();

	private readonly ClientTextMessage _demoMessage = new()
	{
		R1 = 255, G1 = 255, B1 = 255, A1 = 255,
		R2 = 255, G2 = 255, B2 = 255, A2 = 255,
		X = -1f, Y = -1f,
		Name = DemoMessageName, Message = ""
	};

	private readonly ClientTextMessage[] _networkTextMessages =
	[
		new()
		{
			R1 = 255, G1 = 255, B1 = 255, A1 = 255,
			R2 = 255, G2 = 255, B2 = 255, A2 = 255,
			X = -1f, Y = -1f,
			Name = NetworkMessageNames[0], Message = ""
		},
		new(),
		new(),
		new()
	];

	private ClientTextMessage[] _messageTable;

	private static bool IsWhiteSpace( char space ) => space is ' ' or '\t' or '\r' or '\n';

	private static string TrimSpace( string source )
	{
		var start = 0;
		var end = source.Length - 1;

		while ( start < source.Length && IsWhiteSpace( source[start] ) )
			start++;

		while ( end > 0 && IsWhiteSpace( source[end] ) )
			end--;

		var length = end - start + 1;

		return length > 0 ? source.Substring( start, length ) : "";
	}

	private static bool IsToken( string text, string tokenName )
	{
		return string.Compare( text, 1, tokenName, 0, tokenName.Length, StringComparison.OrdinalIgnoreCase ) == 0;
	}

	private static void ParseFloats( string text, Span<float> values )
	{
		var position = 0;

		for ( var i = 0; i < values.Length; i++ )
		{
			while ( position < text.Length && !IsWhiteSpace( text[position] ) )
				position++;

			while ( position < text.Length && IsWhiteSpace( text[position] ) )
				position++;

			values[i] = Common.Atof( text[position..] );
		}
	}

	private bool ParseDirective( string text )
	{
		if ( text.Length == 0 || text[0] != '$' )
			return false;

		Span<float> values = stackalloc float[3];

		if ( IsToken( text, "position" ) )
		{
			ParseFloats( text, values[..2] );

			_messageParms.X = values[0];
			_messageParms.Y = values[1];
		}
		else if ( IsToken( text, "effect" ) )
		{
			ParseFloats( text, values[..1] );

			_messageParms.Effect = (int)values[0];
		}
		else if ( IsToken( text, "fxtime" ) )
		{
			ParseFloats( text, values[..1] );

			_messageParms.FxTime = values[0];
		}
		else if ( IsToken( text, "color2" ) )
		{
			ParseFloats( text, values );

			_messageParms.R2 = (byte)(int)values[0];
			_messageParms.G2 = (byte)(int)values[1];
			_messageParms.B2 = (byte)(int)values[2];
		}
		else if ( IsToken( text, "color" ) )
		{
			ParseFloats( text, values );

			_messageParms.R1 = (byte)(int)values[0];
			_messageParms.G1 = (byte)(int)values[1];
			_messageParms.B1 = (byte)(int)values[2];
		}
		else if ( IsToken( text, "fadein" ) )
		{
			ParseFloats( text, values[..1] );

			_messageParms.FadeIn = values[0];
		}
		else if ( IsToken( text, "fadeout" ) )
		{
			ParseFloats( text, values );

			_messageParms.FadeOut = values[0];
		}
		else if ( IsToken( text, "holdtime" ) )
		{
			ParseFloats( text, values );

			_messageParms.HoldTime = values[0];
		}
		else
		{
			Log.Trace( $"Unknown token: {text}" );
		}

		return true;
	}

	private void TextMessageParse( string file )
	{
		var buffer = file.ToCharArray();
		var messages = new List<ClientTextMessage>();
		var textStarts = new List<int>();
		var name = "";
		var nameHeapSize = 0;
		var lineNumber = 0;
		var filePosition = 0;
		var textStart = 0;
		var inText = false;

		while ( filePosition < buffer.Length )
		{
			var lineEnd = Math.Min( buffer.Length, filePosition + MessageLineLength - 1 );
			var nextPosition = filePosition;

			while ( nextPosition < lineEnd )
			{
				if ( buffer[nextPosition++] == '\n' )
					break;
			}

			if ( messages.Count >= MaxMessages )
				throw new InvalidOperationException( "tmessage::TextMessageParse : messageCount>=MAX_MESSAGES" );

			var trim = TrimSpace( new string( buffer, filePosition, nextPosition - filePosition ) );

			if ( inText )
			{
				if ( trim.StartsWith( '}' ) )
				{
					if ( name.Length + nameHeapSize > MessageNameHeapSize )
					{
						Log.Trace( $"Error parsing file!  length > {MessageNameHeapSize} bytes" );
						return;
					}

					buffer[filePosition - 1] = '\0';

					messages.Add( _messageParms with { Name = name } );
					textStarts.Add( textStart );

					nameHeapSize += name.Length + 1;
					inText = false;
				}
				else if ( trim.StartsWith( '{' ) )
				{
					Log.Trace( $"Unexpected '{{' found, line {lineNumber}" );
					return;
				}
			}
			else if ( trim.Length != 0 && !trim.StartsWith( "//" ) && !ParseDirective( trim ) )
			{
				if ( trim[0] == '{' )
				{
					inText = true;
					textStart = nextPosition;
				}
				else if ( trim[0] == '}' )
				{
					Log.Trace( $"Unexpected '}}' found, line {lineNumber}" );
					return;
				}
				else
				{
					name = trim;
				}
			}

			lineNumber++;
			filePosition = nextPosition;
		}

		Log.Trace( $"Parsed {messages.Count} text messages" );

		for ( var i = 0; i < messages.Count; i++ )
		{
			var end = Array.IndexOf( buffer, '\0', textStarts[i] );

			messages[i].Message = new string( buffer, textStarts[i], (end < 0 ? buffer.Length : end) - textStarts[i] );
		}

		_messageTable = [.. messages];
	}

	private void TextMessageInit()
	{
		_messageTable = null;

		if ( Server.LoadMountText( "titles.txt" ) is { } file )
			TextMessageParse( file );
	}

	public ClientTextMessage TextMessageGet( string name )
	{
		if ( name.Equals( DemoMessageName, StringComparison.OrdinalIgnoreCase ) )
			return _demoMessage;

		for ( var i = 0; i < NetworkMessageNames.Length; i++ )
		{
			if ( name.Equals( NetworkMessageNames[i], StringComparison.OrdinalIgnoreCase ) )
				return _networkTextMessages[i];
		}

		if ( _messageTable is null )
			return null;

		return Array.Find( _messageTable, x => name.Equals( x.Name, StringComparison.OrdinalIgnoreCase ) );
	}

	private void SetDemoMessage( string text, float fadeIn, float fadeOut, float holdTime )
	{
		if ( string.IsNullOrEmpty( text ) )
			return;

		_demoMessage.Message = text;
		_demoMessage.FadeIn = fadeIn;
		_demoMessage.FadeOut = fadeOut;
		_demoMessage.HoldTime = holdTime;
	}

	public void HudMessage( string name ) => _clientDll.DispatchUserMsg( "HudText", Encoding.Latin1.GetBytes( name ) );

	private void ParseTextMessage( MessageReader message )
	{
		var channel = message.ReadByte() % NetworkMessageNames.Length;
		var text = _networkTextMessages[channel];

		text.X = (float)(message.ReadShort() * (1.0 / 8192));
		text.Y = (float)(message.ReadShort() * (1.0 / 8192));
		text.Effect = message.ReadByte();
		text.R1 = (byte)message.ReadByte();
		text.G1 = (byte)message.ReadByte();
		text.B1 = (byte)message.ReadByte();
		text.A1 = (byte)message.ReadByte();
		text.R2 = (byte)message.ReadByte();
		text.G2 = (byte)message.ReadByte();
		text.B2 = (byte)message.ReadByte();
		text.A2 = (byte)message.ReadByte();
		text.FadeIn = (float)(message.ReadWord() * (1.0 / 256));
		text.FadeOut = (float)(message.ReadWord() * (1.0 / 256));
		text.HoldTime = (float)(message.ReadWord() * (1.0 / 256));

		if ( text.Effect == 2 )
			text.FxTime = (float)(message.ReadWord() * (1.0 / 256));

		var value = message.ReadString();

		text.Name = NetworkMessageNames[channel];
		text.Message = value.Length < NetworkMessageLength ? value : value[..(NetworkMessageLength - 1)];

		HudMessage( text.Name );
	}
}
