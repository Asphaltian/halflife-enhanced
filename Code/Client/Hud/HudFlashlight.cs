using GoldSrc;

namespace HalfLife;

public sealed class HudFlashlight( Hud hud ) : HudBase( hud )
{
	private int _sprite1;
	private int _sprite2;
	private int _beam;
	private SpriteRect _rect1;
	private SpriteRect _rect2;
	private SpriteRect _rectBeam;
	private float _batteryFraction;
	private int _battery;
	private bool _on;
	private int _width;

	public override void Init()
	{
		_on = false;

		Hud.Dll.HookUserMsg( "Flashlight", MsgFuncFlashlight );
		Hud.Dll.HookUserMsg( "FlashBat", MsgFuncFlashBat );

		Flags |= Active;

		Hud.AddHudElem( this );
	}

	public override void Reset()
	{
		_on = false;
	}

	public override void VidInit()
	{
		var empty = Hud.GetSpriteIndex( "flash_empty" );
		var full = Hud.GetSpriteIndex( "flash_full" );
		var beam = Hud.GetSpriteIndex( "flash_beam" );

		_sprite1 = Hud.GetSprite( empty );
		_sprite2 = Hud.GetSprite( full );
		_beam = Hud.GetSprite( beam );
		_rect1 = Hud.GetSpriteRect( empty );
		_rect2 = Hud.GetSpriteRect( full );
		_rectBeam = Hud.GetSpriteRect( beam );
		_width = _rect2.Right - _rect2.Left;
	}

	private bool MsgFuncFlashBat( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var x = message.ReadByte();

		_battery = x;
		_batteryFraction = x / 100f;

		return true;
	}

	private bool MsgFuncFlashlight( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_on = message.ReadByte() != 0;

		var x = message.ReadByte();

		_battery = x;
		_batteryFraction = x / 100f;

		return true;
	}

	public override void Draw( float time )
	{
		if ( (Hud.HideHudDisplay & (Hud.HideFlashlight | Hud.HideAll)) != 0 )
			return;

		if ( (Hud.WeaponBits & (1 << Hud.WeaponSuit)) == 0 )
			return;

		var a = _on ? 225 : Hud.MinAlpha;

		Hud.UnpackRgb( out var r, out var g, out var b, _batteryFraction < 0.20 ? Hud.RgbRedish : Hud.RgbYellowish );
		Hud.ScaleColors( ref r, ref g, ref b, a );

		var y = (_rect1.Bottom - _rect2.Top) / 2;
		var x = Hud.ScreenWidth - _width - (_width / 2);

		Engine.SpriteSet( _sprite1, r, g, b );
		Engine.SpriteDrawAdditive( 0, x, y, _rect1 );

		if ( _on )
		{
			x = Hud.ScreenWidth - (_width / 2);

			Engine.SpriteSet( _beam, r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y, _rectBeam );
		}

		x = Hud.ScreenWidth - _width - (_width / 2);

		var offset = (int)(_width * (1.0 - _batteryFraction));

		if ( offset >= _width )
			return;

		Engine.SpriteSet( _sprite2, r, g, b );
		Engine.SpriteDrawAdditive( 0, x + offset, y, _rect2 with { Left = _rect2.Left + offset } );
	}
}
