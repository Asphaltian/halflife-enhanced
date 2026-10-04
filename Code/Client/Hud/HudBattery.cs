using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudBattery( Hud hud ) : HudBase( hud )
{
	private int _sprite1;
	private int _sprite2;
	private SpriteRect _rect1;
	private SpriteRect _rect2;
	private int _battery;
	private float _fade;
	private int _height;

	public override void Init()
	{
		_battery = 0;
		_fade = 0f;
		Flags = 0;

		Hud.Dll.HookUserMsg( "Battery", MsgFuncBattery );

		Hud.AddHudElem( this );
	}

	public override void VidInit()
	{
		_sprite1 = _sprite2 = 0;
		_rect1 = Hud.GetSpriteRect( Hud.GetSpriteIndex( "suit_empty" ) );
		_rect2 = Hud.GetSpriteRect( Hud.GetSpriteIndex( "suit_full" ) );
		_height = _rect2.Bottom - _rect1.Top;
		_fade = 0f;
	}

	private bool MsgFuncBattery( string name, byte[] data )
	{
		Flags |= Active;

		var message = new MessageBuffer( data );
		var x = message.ReadShort();

		if ( x != _battery )
		{
			_fade = Hud.FadeTime;
			_battery = x;
		}

		return true;
	}

	public override void Draw( float time )
	{
		if ( (Hud.HideHudDisplay & Hud.HideHealth) != 0 )
			return;

		int a;
		var rect = _rect2 with { Top = _rect2.Top + (int)(_height * ((100 - Math.Min( 100, _battery )) * 0.01)) };

		Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );

		if ( (Hud.WeaponBits & (1 << Hud.WeaponSuit)) == 0 )
			return;

		if ( _fade != 0f )
		{
			if ( _fade > Hud.FadeTime )
				_fade = Hud.FadeTime;

			_fade -= (float)(Hud.TimeDelta * 20);

			if ( _fade <= 0f )
				_fade = 0f;

			a = (int)(Hud.MinAlpha + (_fade / Hud.FadeTime * 128));
		}
		else
		{
			a = Hud.MinAlpha;
		}

		Hud.ScaleColors( ref r, ref g, ref b, a );

		var offset = (_rect1.Bottom - _rect1.Top) / 6;
		var y = Hud.ScreenHeight - Hud.FontHeight - (Hud.FontHeight / 2);
		var width = _rect1.Right - _rect1.Left;
		var x = 3 * width;

		if ( _sprite1 == 0 )
			_sprite1 = Hud.GetSprite( Hud.GetSpriteIndex( "suit_empty" ) );

		if ( _sprite2 == 0 )
			_sprite2 = Hud.GetSprite( Hud.GetSpriteIndex( "suit_full" ) );

		Engine.SpriteSet( _sprite1, r, g, b );
		Engine.SpriteDrawAdditive( 0, x, y - offset, _rect1 );

		if ( rect.Bottom > rect.Top )
		{
			Engine.SpriteSet( _sprite2, r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y - offset + (rect.Top - _rect2.Top), rect );
		}

		x += width;
		y += (int)(Hud.FontHeight * 0.2f);

		Hud.DrawHudNumber( x, y, Hud.Draw3Digits | Hud.DrawZero, _battery, r, g, b );
	}
}
