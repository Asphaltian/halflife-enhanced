using System;

namespace HalfLife;

public sealed class HudAmmoSecondary( Hud hud ) : HudBase( hud )
{
	private const int MaxSecondaryAmmoValues = 4;

	private readonly int[] _ammoAmounts = new int[MaxSecondaryAmmoValues];

	private int _ammoIcon;
	private float _fade;

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "SecAmmoVal", MsgFuncSecAmmoVal );
		Hud.Dll.HookUserMsg( "SecAmmoIcon", MsgFuncSecAmmoIcon );

		Hud.AddHudElem( this );

		_ammoIcon = 0;

		Array.Fill( _ammoAmounts, -1 );

		Reset();
	}

	public override void Reset()
	{
		_fade = 0f;
	}

	public override void Draw( float time )
	{
		if ( (Hud.HideHudDisplay & (Hud.HideWeapons | Hud.HideAll)) != 0 )
			return;

		Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );

		var a = Math.Max( Hud.MinAlpha, (int)_fade );

		if ( _fade > 0f )
			_fade -= (float)(Hud.TimeDelta * 20);

		Hud.ScaleColors( ref r, ref g, ref b, a );

		var number = Hud.GetSpriteRect( Hud.NumberZero );
		var ammoWidth = number.Right - number.Left;
		var y = Hud.ScreenHeight - (Hud.FontHeight * 4);
		var x = Hud.ScreenWidth - ammoWidth;

		if ( _ammoIcon != 0 )
		{
			var icon = Hud.GetSpriteRect( _ammoIcon );

			x -= icon.Right - icon.Left;
			y -= icon.Top - icon.Bottom;

			Engine.SpriteSet( Hud.GetSprite( _ammoIcon ), r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y, icon );
		}
		else
		{
			x -= ammoWidth;
			y -= number.Top - number.Bottom;
		}

		for ( var i = MaxSecondaryAmmoValues - 1; i >= 0; i-- )
		{
			if ( _ammoAmounts[i] < 0 )
				continue;

			x -= ammoWidth / 2;
			x -= Hud.GetNumWidth( _ammoAmounts[i], Hud.DrawZero ) * ammoWidth;

			Hud.DrawHudNumber( x, y, Hud.DrawZero, _ammoAmounts[i], r, g, b );

			if ( i == 0 )
				continue;

			x -= ammoWidth / 2;

			Engine.FillRgba( x, y, ammoWidth / 10, Hud.FontHeight, r, g, b, a );
		}
	}

	private bool MsgFuncSecAmmoIcon( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_ammoIcon = Hud.GetSpriteIndex( message.ReadString() );

		return true;
	}

	private bool MsgFuncSecAmmoVal( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var index = message.ReadByte();

		if ( index is < 0 or >= MaxSecondaryAmmoValues )
			return true;

		_ammoAmounts[index] = message.ReadByte();
		Flags |= Active;

		var count = 0;

		foreach ( var amount in _ammoAmounts )
			count += Math.Max( 0, amount );

		if ( count == 0 )
		{
			Flags &= ~Active;
			return true;
		}

		_fade = 200f;

		return true;
	}
}
