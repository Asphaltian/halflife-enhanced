using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudSayText( Hud hud ) : HudBase( hud )
{
	private const int MaxLines = 5;
	private const int MaxCharsPerLine = 256;
	private const int LineStart = 10;
	private const int MaxPlayerNameLength = 32;

	private readonly string[] _lineBuffer = new string[MaxLines + 1];
	private readonly float[][] _nameColors = new float[MaxLines + 1][];
	private readonly int[] _nameLengths = new int[MaxLines + 1];

	private float _scrollTime;
	private int _startY;
	private int _lineHeight;

	[ConVar( "hud_saytext" )]
	public static float SayText { get; set; } = 1f;

	[ConVar( "hud_saytext_time" )]
	private static float SayTextTime { get; set; } = 5f;

	private int MaxLineWidth => Hud.ScreenWidth - 40;

	public override void Init()
	{
		Hud.AddHudElem( this );

		Hud.Dll.HookUserMsg( "SayText", MsgFuncSayText );

		InitHudData();

		Flags |= Intermission;
	}

	public override void InitHudData()
	{
		Array.Fill( _lineBuffer, "" );
		Array.Clear( _nameColors );
		Array.Clear( _nameLengths );
	}

	private int ScrollTextUp()
	{
		Engine.ConsolePrint( _lineBuffer[0] );

		for ( var i = 0; i < MaxLines; i++ )
		{
			_lineBuffer[i] = _lineBuffer[i + 1];
			_nameColors[i] = _nameColors[i + 1];
			_nameLengths[i] = _nameLengths[i + 1];
		}

		_lineBuffer[MaxLines] = "";
		_lineBuffer[MaxLines - 1] = "";

		if ( _lineBuffer[0].StartsWith( ' ' ) )
		{
			_lineBuffer[0] = $"\u0002{_lineBuffer[0][1..]}";
			return 1 + ScrollTextUp();
		}

		return 1;
	}

	public override void Draw( float time )
	{
		var y = _startY;

		if ( (Hud.ViewPort is { } viewPort && !viewPort.AllowedToPrintText()) || SayText == 0f )
			return;

		_scrollTime = Math.Min( _scrollTime, time + SayTextTime );

		if ( _scrollTime <= time )
		{
			if ( _lineBuffer[0].Length > 0 )
			{
				_scrollTime = time + SayTextTime;
				ScrollTextUp();
			}
			else
			{
				Flags &= ~Active;
			}
		}

		for ( var i = 0; i < MaxLines; i++ )
		{
			var line = _lineBuffer[i];

			if ( line.Length > 0 )
			{
				if ( line[0] == 2 && _nameColors[i] is { } color )
				{
					var nameLength = Math.Min( Math.Min( _nameLengths[i], MaxPlayerNameLength + 31 ), line.Length );
					var name = line[..nameLength];
					var rest = line[Math.Min( _nameLengths[i], line.Length )..];

					Engine.DrawSetTextColor( color[0], color[1], color[2] );

					var x = Engine.DrawConsoleString( LineStart, y, name[1..] );

					Engine.DrawConsoleString( x, y, rest.Length > 0 ? rest[..^1] : rest );
				}
				else
				{
					Engine.DrawConsoleString( LineStart, y, line );
				}
			}

			y += _lineHeight;
		}
	}

	private bool MsgFuncSayText( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var clientIndex = message.ReadByte();

		SayTextPrint( message.ReadString(), data.Length - 1, clientIndex );

		return true;
	}

	public void SayTextPrint( string text, int bufferSize, int clientIndex = -1 )
	{
		if ( Hud.ViewPort is { } viewPort && !viewPort.AllowedToPrintText() )
		{
			Engine.ConsolePrint( text );
			return;
		}

		int i;

		for ( i = 0; i < MaxLines; i++ )
		{
			if ( _lineBuffer[i].Length == 0 )
				break;
		}

		if ( i == MaxLines )
		{
			ScrollTextUp();
			i = MaxLines - 1;
		}

		_nameLengths[i] = 0;
		_nameColors[i] = null;

		if ( text.Length > 0 && text[0] == 2 && clientIndex > 0 && Engine.GetPlayerName( clientIndex ) is { } playerName )
		{
			var nameInString = text.IndexOf( playerName, StringComparison.Ordinal );

			if ( nameInString >= 0 )
			{
				_nameLengths[i] = playerName.Length + nameInString;
				_nameColors[i] = Hud.GetClientColor( clientIndex );
			}
		}

		var size = Math.Max( bufferSize, MaxCharsPerLine );

		_lineBuffer[i] = text.Length > size ? text[..size] : text;

		EnsureTextFitsInOneLineAndWrapIfHaveTo( i );

		if ( i == 0 )
			_scrollTime = Hud.Time + SayTextTime;

		Flags |= Active;
		Engine.PlaySoundByName( "misc/talk.wav", 1f );

		_startY = Hud.ScreenHeight - 60 - (_lineHeight * (MaxLines + 2));
	}

	private void EnsureTextFitsInOneLineAndWrapIfHaveTo( int line )
	{
		Engine.DrawConsoleStringLen( _lineBuffer[line], out var lineWidth, out _lineHeight );

		if ( lineWidth + LineStart <= MaxLineWidth )
			return;

		var text = _lineBuffer[line];
		var length = LineStart;
		var lastBreak = -1;

		for ( var x = 0; x < text.Length; x++ )
		{
			if ( text[x] == '/' && x + 1 < text.Length && text[x + 1] == '(' )
			{
				x += 2;

				while ( x < text.Length && text[x] != ')' )
					x++;

				if ( x < text.Length )
					x++;

				if ( x >= text.Length )
					break;
			}

			if ( text[x] == ' ' && x != 0 )
				lastBreak = x;

			Engine.DrawConsoleStringLen( text[x].ToString(), out var characterWidth, out _lineHeight );
			length += characterWidth;

			if ( length <= MaxLineWidth )
				continue;

			if ( lastBreak < 0 )
				lastBreak = x - 1;

			int j;

			do
			{
				for ( j = 0; j < MaxLines; j++ )
				{
					if ( _lineBuffer[j].Length == 0 )
						break;
				}

				if ( j == MaxLines )
					line -= ScrollTextUp();
			}
			while ( j == MaxLines );

			var remaining = text[lastBreak..];

			if ( remaining.StartsWith( ' ' ) )
				_lineBuffer[j] += remaining;

			if ( line >= 0 )
				_lineBuffer[line] = text[..lastBreak];

			EnsureTextFitsInOneLineAndWrapIfHaveTo( j );
			break;
		}
	}
}
