using System;
using System.Text;
using GoldSrc;

namespace HalfLife;

public sealed class HudStatusBar( Hud hud ) : HudBase( hud )
{
	private const int MaxStatusTextLength = 128;
	private const int MaxStatusBarValues = 8;
	private const int MaxStatusBarLines = 3;
	private const int StatusBarIdLine = 1;

	private readonly string[] _statusText = new string[MaxStatusBarLines];
	private readonly string[] _statusBar = new string[MaxStatusBarLines];
	private readonly int[] _statusValues = new int[MaxStatusBarValues];
	private readonly float[][] _nameColors = new float[MaxStatusBarLines][];

	private bool _reparseString;

	[ConVar( "hud_centerid" )]
	private static float CenterId { get; set; } = 0f;

	public override void Init()
	{
		Hud.AddHudElem( this );

		Hud.Dll.HookUserMsg( "StatusText", MsgFuncStatusText );
		Hud.Dll.HookUserMsg( "StatusValue", MsgFuncStatusValue );

		Array.Fill( _statusBar, "" );

		Reset();
	}

	public override void Reset()
	{
		Flags &= ~Active;

		Array.Fill( _statusText, "" );
		Array.Clear( _statusValues );

		_statusValues[0] = 1;

		Array.Fill( _nameColors, HudDeathNotice.ColorYellow );
	}

	private void ParseStatusString( int lineNumber )
	{
		var source = Hud.TextMessage.LocaliseTextString( _statusText[lineNumber], MaxStatusTextLength );
		var bar = new StringBuilder();
		var position = 0;

		while ( Peek( source, position ) != '\0' )
		{
			while ( Peek( source, position ) == '\n' )
				position++;

			if ( position >= MaxStatusTextLength || bar.Length >= MaxStatusTextLength )
				break;

			var index = Atoi( source, position );

			if ( index >= 0 && index < MaxStatusBarValues && _statusValues[index] != 0 )
			{
				while ( char.IsAsciiDigit( Peek( source, position ) ) )
					position++;

				if ( Peek( source, position ) is '\n' or '\0' )
					continue;

				while ( Peek( source, position ) is not '\n' and not '\0' )
				{
					if ( source[position] != '%' )
					{
						bar.Append( source[position++] );
						continue;
					}

					var valueType = Peek( source, ++position );

					if ( valueType == '%' )
					{
						bar.Append( valueType );
						position++;
						continue;
					}

					index = Atoi( source, ++position );

					while ( char.IsAsciiDigit( Peek( source, position ) ) )
						position++;

					if ( index < 0 || index >= MaxStatusBarValues )
						continue;

					var value = _statusValues[index];
					var replacement = "";

					switch ( valueType )
					{
						case 'p':
							if ( Engine.GetPlayerName( value ) is { } playerName )
							{
								replacement = playerName.Length > Hud.MaxPlayerNameLength ? playerName[..Hud.MaxPlayerNameLength] : playerName;
								_nameColors[lineNumber] = Hud.GetClientColor( value );
							}
							else
							{
								replacement = "******";
							}

							break;

						case 'i':
							replacement = value.ToString();
							break;
					}

					for ( var i = 0; i < replacement.Length && bar.Length < MaxStatusTextLength; i++ )
						bar.Append( replacement[i] );
				}
			}
			else
			{
				while ( Peek( source, position ) is not '\0' and not '\n' )
					position++;
			}
		}

		_statusBar[lineNumber] = bar.ToString();
	}

	private static char Peek( string text, int position ) => position < text.Length ? text[position] : '\0';

	private static int Atoi( string text, int position ) => position < text.Length ? Common.Atoi( text[position..] ) : 0;

	public override void Draw( float time )
	{
		if ( _reparseString )
		{
			for ( var i = 0; i < MaxStatusBarLines; i++ )
			{
				_nameColors[i] = HudDeathNotice.ColorYellow;
				ParseStatusString( i );
			}

			_reparseString = false;
		}

		var startY = Hud.ScreenHeight - 52;

		for ( var i = 0; i < MaxStatusBarLines; i++ )
		{
			Engine.DrawConsoleStringLen( _statusBar[i], out var textWidth, out var textHeight );

			var x = 8;
			var y = startY - (4 + (textHeight * i));

			if ( i == StatusBarIdLine && CenterId != 0f )
			{
				x = Math.Max( 0, Math.Max( 2, Hud.ScreenWidth - textWidth ) / 2 );
				y = (int)((Hud.ScreenHeight / 2) + (textHeight * CenterId));
			}

			if ( _nameColors[i] is { } color )
				Engine.DrawSetTextColor( color[0], color[1], color[2] );

			Engine.DrawConsoleString( x, y, _statusBar[i] );
		}
	}

	private bool MsgFuncStatusText( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var line = message.ReadByte();

		if ( line < 0 || line > MaxStatusBarLines )
			return true;

		if ( line < MaxStatusBarLines )
		{
			var text = message.ReadString();

			_statusText[line] = text.Length >= MaxStatusTextLength ? text[..(MaxStatusTextLength - 1)] : text;
		}

		Flags |= Active;
		_reparseString = true;

		return true;
	}

	private bool MsgFuncStatusValue( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var index = message.ReadByte();

		if ( index < 1 || index > MaxStatusBarValues )
			return true;

		if ( index < MaxStatusBarValues )
			_statusValues[index] = message.ReadShort();

		_reparseString = true;

		return true;
	}
}
