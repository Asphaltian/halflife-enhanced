using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudMessage( Hud hud ) : HudBase( hud )
{
	private const int MaxHudMessages = 16;
	private const int MaxLineLength = 80;
	private const string EndingMessage = "END3";

	private struct MessageParms
	{
		public ClientTextMessage Message;
		public float Time;
		public int X;
		public int Y;
		public int TotalWidth;
		public int TotalHeight;
		public int Width;
		public int Lines;
		public int LineLength;
		public int Length;
		public int R;
		public int G;
		public int B;
		public int Text;
		public int FadeBlend;
		public float CharTime;
		public float FadeTime;
	}

	private readonly ClientTextMessage[] _messages = new ClientTextMessage[MaxHudMessages];
	private readonly float[] _startTime = new float[MaxHudMessages];
	private readonly char[] _line = new char[MaxLineLength];
	private readonly ClientTextMessage _customMessage = new();

	private MessageParms _parms;
	private float _gameTitleTime;
	private ClientTextMessage _gameTitle;
	private int _titleLife;
	private int _titleHalf;
	private bool _endAfterMessage;

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "HudText", MsgFuncHudText );
		Hud.Dll.HookUserMsg( "GameTitle", MsgFuncGameTitle );

		Hud.AddHudElem( this );

		Reset();
	}

	public override void VidInit()
	{
		_titleHalf = Hud.GetSpriteIndex( "title_half" );
		_titleLife = Hud.GetSpriteIndex( "title_life" );
	}

	public override void Reset()
	{
		Array.Clear( _messages );
		Array.Clear( _startTime );

		_endAfterMessage = false;
		_gameTitleTime = 0f;
		_gameTitle = null;
	}

	private static float FadeBlend( float fadeIn, float fadeOut, float hold, float localTime )
	{
		var fadeTime = fadeIn + hold;

		if ( localTime < 0f )
			return 0f;

		if ( localTime < fadeIn )
			return 1f - ((fadeIn - localTime) / fadeIn);

		if ( localTime > fadeTime )
			return fadeOut > 0f ? 1f - ((localTime - fadeTime) / fadeOut) : 0f;

		return 1f;
	}

	private int XPosition( float x, int width, int totalWidth )
	{
		int position;

		if ( x == -1f )
			position = (Hud.ScreenWidth - width) / 2;
		else if ( x < 0f )
			position = (int)(((1.0 + x) * Hud.ScreenWidth) - totalWidth);
		else
			position = (int)(x * Hud.ScreenWidth);

		if ( position + width > Hud.ScreenWidth )
			position = Hud.ScreenWidth - width;
		else if ( position < 0 )
			position = 0;

		return position;
	}

	private int YPosition( float y, int height )
	{
		int position;

		if ( y == -1f )
			position = (int)((Hud.ScreenHeight - height) * 0.5);
		else if ( y < 0f )
			position = (int)(((1.0 + y) * Hud.ScreenHeight) - height);
		else
			position = (int)(y * Hud.ScreenHeight);

		if ( position + height > Hud.ScreenHeight )
			position = Hud.ScreenHeight - height;
		else if ( position < 0 )
			position = 0;

		return position;
	}

	private void MessageScanNextChar()
	{
		var message = _parms.Message;
		int sourceRed = message.R1;
		int sourceGreen = message.G1;
		int sourceBlue = message.B1;
		var destRed = 0;
		var destGreen = 0;
		var destBlue = 0;
		var blend = 0;

		switch ( message.Effect )
		{
			case 0:
			case 1:
				blend = _parms.FadeBlend;
				break;

			case 2:
				_parms.CharTime += message.FadeIn;

				if ( _parms.CharTime > _parms.Time )
				{
					sourceRed = sourceGreen = sourceBlue = 0;
					blend = 0;
				}
				else
				{
					var deltaTime = _parms.Time - _parms.CharTime;

					if ( _parms.Time > _parms.FadeTime )
					{
						blend = _parms.FadeBlend;
					}
					else if ( deltaTime > message.FxTime )
					{
						blend = 0;
					}
					else
					{
						destRed = message.R2;
						destGreen = message.G2;
						destBlue = message.B2;
						blend = (int)(255 - ((deltaTime * (1.0 / message.FxTime) * 255.0) + 0.5));
					}
				}

				break;
		}

		blend = Math.Clamp( blend, 0, 255 );

		_parms.R = ((sourceRed * (255 - blend)) + (destRed * blend)) >> 8;
		_parms.G = ((sourceGreen * (255 - blend)) + (destGreen * blend)) >> 8;
		_parms.B = ((sourceBlue * (255 - blend)) + (destBlue * blend)) >> 8;

		if ( message.Effect != 1 || _parms.CharTime == 0f )
			return;

		if ( _parms.X >= 0 && _parms.Y >= 0 && _parms.X + Engine.CharacterWidth( (char)_parms.Text ) <= Hud.ScreenWidth )
			Engine.DrawCharacter( _parms.X, _parms.Y, _parms.Text, message.R2, message.G2, message.B2 );
	}

	private void MessageScanStart()
	{
		var message = _parms.Message;

		switch ( message.Effect )
		{
			case 1:
			case 0:
				_parms.FadeTime = message.FadeIn + message.HoldTime;

				if ( _parms.Time < message.FadeIn )
					_parms.FadeBlend = (int)((message.FadeIn - _parms.Time) * (1.0 / message.FadeIn) * 255);
				else if ( _parms.Time > _parms.FadeTime )
					_parms.FadeBlend = message.FadeOut > 0f ? (int)((_parms.Time - _parms.FadeTime) / message.FadeOut * 255) : 255;
				else
					_parms.FadeBlend = 0;

				_parms.CharTime = 0f;

				if ( message.Effect == 1 && Hud.Rand() % 100 < 10 )
					_parms.CharTime = 1f;

				break;

			case 2:
				_parms.FadeTime = (message.FadeIn * _parms.Length) + message.HoldTime;

				if ( _parms.Time > _parms.FadeTime && message.FadeOut > 0f )
					_parms.FadeBlend = (int)((_parms.Time - _parms.FadeTime) / message.FadeOut * 255);
				else
					_parms.FadeBlend = 0;

				break;
		}
	}

	private void MessageDrawScan( ClientTextMessage message, float time )
	{
		var text = message.Message;
		var width = 0;

		_parms.Lines = 1;
		_parms.Time = time;
		_parms.Message = message;
		_parms.TotalWidth = 0;

		foreach ( var character in text )
		{
			if ( character == '\n' )
			{
				_parms.Lines++;

				if ( width > _parms.TotalWidth )
					_parms.TotalWidth = width;

				width = 0;
			}
			else
			{
				width += Engine.CharacterWidth( character );
			}
		}

		_parms.Length = text.Length;
		_parms.TotalHeight = _parms.Lines * Engine.CharacterHeight;

		_parms.Y = YPosition( message.Y, _parms.TotalHeight );
		_parms.CharTime = 0f;

		MessageScanStart();

		var position = 0;

		for ( var i = 0; i < _parms.Lines; i++ )
		{
			_parms.LineLength = 0;
			_parms.Width = 0;

			while ( position < text.Length && text[position] != '\n' && _parms.LineLength < MaxLineLength - 1 )
			{
				var character = text[position];

				_line[_parms.LineLength] = character;
				_parms.Width += Engine.CharacterWidth( character );
				_parms.LineLength++;
				position++;
			}

			position++;

			_parms.X = XPosition( message.X, _parms.Width, _parms.TotalWidth );

			for ( var j = 0; j < _parms.LineLength; j++ )
			{
				_parms.Text = _line[j];

				var next = _parms.X + Engine.CharacterWidth( _line[j] );

				MessageScanNextChar();

				if ( _parms.X >= 0 && _parms.Y >= 0 && next <= Hud.ScreenWidth )
					Engine.DrawCharacter( _parms.X, _parms.Y, _parms.Text, _parms.R, _parms.G, _parms.B );

				_parms.X = next;
			}

			_parms.Y += Engine.CharacterHeight;
		}
	}

	public override void Draw( float time )
	{
		var drawn = 0;
		var endTime = 0f;

		if ( _gameTitleTime > 0f )
		{
			var localTime = Hud.Time - _gameTitleTime;

			if ( _gameTitleTime > Hud.Time )
				_gameTitleTime = Hud.Time;

			if ( localTime > _gameTitle.FadeIn + _gameTitle.HoldTime + _gameTitle.FadeOut )
			{
				_gameTitleTime = 0f;
			}
			else
			{
				var brightness = FadeBlend( _gameTitle.FadeIn, _gameTitle.FadeOut, _gameTitle.HoldTime, localTime );
				var half = Hud.GetSpriteRect( _titleHalf );
				var life = Hud.GetSpriteRect( _titleLife );
				var halfWidth = half.Right - half.Left;
				var fullWidth = halfWidth + life.Right - life.Left;
				var fullHeight = half.Bottom - half.Top;
				var x = XPosition( _gameTitle.X, fullWidth, fullWidth );
				var y = YPosition( _gameTitle.Y, fullHeight );
				var r = (int)(brightness * _gameTitle.R1);
				var g = (int)(brightness * _gameTitle.G1);
				var b = (int)(brightness * _gameTitle.B1);

				Engine.SpriteSet( Hud.GetSprite( _titleHalf ), r, g, b );
				Engine.SpriteDrawAdditive( 0, x, y, half );

				Engine.SpriteSet( Hud.GetSprite( _titleLife ), r, g, b );
				Engine.SpriteDrawAdditive( 0, x + halfWidth, y, life );

				drawn = 1;
			}
		}

		for ( var i = 0; i < MaxHudMessages; i++ )
		{
			if ( _messages[i] is not null && _startTime[i] > Hud.Time )
				_startTime[i] = (float)(Hud.Time + _parms.Time - _startTime[i] + 0.2);
		}

		for ( var i = 0; i < MaxHudMessages; i++ )
		{
			if ( _messages[i] is not { } message )
				continue;

			switch ( message.Effect )
			{
				case 0:
				case 1:
					endTime = _startTime[i] + message.FadeIn + message.FadeOut + message.HoldTime;
					break;

				case 2:
					endTime = _startTime[i] + (message.FadeIn * message.Message.Length) + message.FadeOut + message.HoldTime;
					break;
			}

			if ( time <= endTime )
			{
				MessageDrawScan( message, time - _startTime[i] );

				drawn++;
			}
			else
			{
				_messages[i] = null;

				if ( _endAfterMessage )
					Client.ClientCmd( "wait\nwait\nwait\nwait\nwait\nwait\nwait\ndisconnect\n" );
			}
		}

		_parms.Time = Hud.Time;

		if ( drawn == 0 )
			Flags &= ~Active;
	}

	public void MessageAdd( ClientTextMessage newMessage )
	{
		_parms.Time = Hud.Time;

		Flags |= Active;

		for ( var i = 0; i < MaxHudMessages; i++ )
		{
			if ( _messages[i] is not null )
				continue;

			_messages[i] = newMessage;
			_startTime[i] = Hud.Time;

			return;
		}
	}

	private void MessageAdd( string name, float time )
	{
		for ( var i = 0; i < MaxHudMessages; i++ )
		{
			if ( _messages[i] is not null )
				continue;

			var message = Engine.TextMessageGet( name.StartsWith( '#' ) ? name[1..] : name );

			if ( message is null )
			{
				_customMessage.Effect = 2;
				_customMessage.R1 = _customMessage.G1 = _customMessage.B1 = _customMessage.A1 = 100;
				_customMessage.R2 = 240;
				_customMessage.G2 = 110;
				_customMessage.B2 = 0;
				_customMessage.A2 = 0;
				_customMessage.X = -1f;
				_customMessage.Y = 0.7f;
				_customMessage.FadeIn = 0.01f;
				_customMessage.FadeOut = 1.5f;
				_customMessage.FxTime = 0.25f;
				_customMessage.HoldTime = 5f;
				_customMessage.Name = "Custom";
				_customMessage.Message = name;

				message = _customMessage;
			}

			for ( var j = 0; j < MaxHudMessages; j++ )
			{
				if ( _messages[j] is not { } other )
					continue;

				if ( message.Message == other.Message )
					return;

				if ( MathF.Abs( message.Y - other.Y ) < 0.0001f && MathF.Abs( message.X - other.X ) < 0.0001f )
					_messages[j] = null;
			}

			_messages[i] = message;
			_startTime[i] = time;

			return;
		}
	}

	private bool MsgFuncHudText( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var text = message.ReadString();

		if ( text == EndingMessage )
			_endAfterMessage = true;

		MessageAdd( text, Hud.Time );

		_parms.Time = Hud.Time;

		Flags |= Active;

		return true;
	}

	private bool MsgFuncGameTitle( string name, byte[] data )
	{
		_gameTitle = Engine.TextMessageGet( "GAMETITLE" );

		if ( _gameTitle is not null )
		{
			_gameTitleTime = Hud.Time;

			Flags |= Active;
		}

		return true;
	}
}
