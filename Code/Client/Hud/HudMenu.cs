using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudMenu( Hud hud ) : HudBase( hud )
{
	private const int MaxMenuString = 512;
	private const int MaxMenuLine = 80;

	private string _menuString = "";
	private string _prelocalisedMenuString = "";

	private int _validSlots;
	private float _shutoffTime;
	private bool _waitingForMore;

	private int _r;
	private int _g;
	private int _b;
	private int _x;
	private bool _rightAlign;

	public bool MenuDisplayed { get; private set; }

	public override void Init()
	{
		Hud.AddHudElem( this );

		Hud.Dll.HookUserMsg( "ShowMenu", MsgFuncShowMenu );

		InitHudData();
	}

	public override void InitHudData()
	{
		MenuDisplayed = false;
		_validSlots = 0;

		Reset();
	}

	public override void Reset()
	{
		_prelocalisedMenuString = "";
		_waitingForMore = false;
	}

	private int ParseEscapeToken( int token )
	{
		if ( _menuString[token] != '\\' )
			return token;

		token++;

		if ( token >= _menuString.Length )
			return token;

		switch ( _menuString[token] )
		{
			case 'w':
				_r = 255;
				_g = 255;
				_b = 255;
				break;

			case 'd':
				_r = 100;
				_g = 100;
				_b = 100;
				break;

			case 'y':
				_r = 255;
				_g = 210;
				_b = 64;
				break;

			case 'r':
				_r = 210;
				_g = 24;
				_b = 0;
				break;

			case 'R':
				_x = Hud.ScreenWidth / 2;
				_rightAlign = true;
				break;
		}

		return token + 1;
	}

	public override void Draw( float time )
	{
		if ( _shutoffTime > 0f && _shutoffTime <= Hud.Time )
		{
			MenuDisplayed = false;
			Flags &= ~Active;
			return;
		}

		if ( Hud.ViewPort is { } viewPort && viewPort.IsScoreBoardVisible() )
			return;

		var newlines = 0;

		foreach ( var character in _menuString )
		{
			if ( character == '\n' )
				newlines++;
		}

		var fontHeight = Math.Max( 12, Engine.CharacterHeight );
		var y = (Hud.ScreenHeight / 2) - (newlines / 2 * fontHeight) - ((3 * fontHeight) + (fontHeight / 3));

		_r = 255;
		_g = 255;
		_b = 255;
		_x = 20;
		_rightAlign = false;

		var position = 0;

		while ( position < _menuString.Length )
		{
			if ( _menuString[position] == '\\' )
			{
				position = ParseEscapeToken( position );
			}
			else if ( _menuString[position] == '\n' )
			{
				_rightAlign = false;
				_x = 20;
				y += fontHeight;

				position++;
			}
			else
			{
				var start = position;

				while ( position < _menuString.Length && _menuString[position] is not '\n' and not '\\' )
					position++;

				var text = _menuString.Substring( start, Math.Min( position - start, MaxMenuLine - 1 ) );

				_x = _rightAlign
					? Hud.DrawHudStringReverse( _x, y, 0, text, _r, _g, _b )
					: Hud.DrawHudString( _x, y, 320, text, _r, _g, _b );
			}
		}
	}

	public void SelectMenuItem( int menuItem )
	{
		if ( menuItem <= 0 || (_validSlots & (1 << (menuItem - 1))) == 0 )
			return;

		Client.ClientCmd( $"menuselect {menuItem}\n" );

		MenuDisplayed = false;
		Flags &= ~Active;
	}

	private static string Truncate( string text ) => text.Length >= MaxMenuString ? text[..(MaxMenuString - 1)] : text;

	private bool MsgFuncShowMenu( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_validSlots = message.ReadShort();

		var displayTime = message.ReadChar();
		var needMore = message.ReadByte() != 0;

		_shutoffTime = displayTime > 0 ? displayTime + Hud.Time : -1f;

		if ( _validSlots != 0 )
		{
			var text = message.ReadString();

			_prelocalisedMenuString = Truncate( _waitingForMore ? _prelocalisedMenuString + text : text );

			if ( !needMore )
			{
				_menuString = Truncate( Hud.TextMessage.BufferedLocaliseTextString( _prelocalisedMenuString ) );
				_menuString = Truncate( Hud.Dll.ConvertString( _menuString ) );
			}

			MenuDisplayed = true;
			Flags |= Active;
		}
		else
		{
			MenuDisplayed = false;
			Flags &= ~Active;
		}

		_waitingForMore = needMore;

		return true;
	}
}
