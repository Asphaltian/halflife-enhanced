using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudHealth( Hud hud ) : HudBase( hud )
{
	private const int DamageImageLife = 2;
	private const int NumDamageTypes = 12;

	private const int DamageBurn = 1 << 3;
	private const int DamageFreeze = 1 << 4;
	private const int DamageShock = 1 << 8;
	private const int DamageSonic = 1 << 9;
	private const int DamageDrown = 1 << 14;
	private const int DamageNerveGas = 1 << 16;
	private const int DamagePoison = 1 << 17;
	private const int DamageRadiation = 1 << 18;
	private const int DamageAcid = 1 << 20;
	private const int DamageSlowBurn = 1 << 21;
	private const int DamageSlowFreeze = 1 << 22;
	private const int DamageMortar = 1 << 23;
	private const int DamageCaltrop = 1 << 30;
	private const int DamageHalluc = 1 << 31;

	private static readonly int[] DamageFlags =
	[
		DamagePoison,
		DamageAcid,
		DamageFreeze | DamageSlowFreeze,
		DamageDrown,
		DamageBurn | DamageSlowBurn,
		DamageNerveGas,
		DamageRadiation,
		DamageShock,
		DamageCaltrop,
		DamageMortar,
		DamageSonic,
		DamageHalluc
	];

	private struct DamageImage
	{
		public float Expire;
		public float Baseline;
		public int X;
		public int Y;
	}

	private readonly DamageImage[] _damage = new DamageImage[NumDamageTypes];

	private int _damageHeight;
	private int _damageWidth;
	private int _sprite;
	private int _bitsDamage;
	private int _damageBio;
	private int _cross;
	private float _attackFront;
	private float _attackRear;
	private float _attackLeft;
	private float _attackRight;

	public int Health { get; private set; }

	public float Fade { get; set; }

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "Health", MsgFuncHealth );
		Hud.Dll.HookUserMsg( "Damage", MsgFuncDamage );

		Health = 100;
		Fade = 0f;
		Flags = 0;
		_bitsDamage = 0;
		_attackFront = _attackRear = _attackRight = _attackLeft = 0f;
		_damageHeight = 0;
		_damageWidth = 0;

		Array.Clear( _damage );

		Hud.AddHudElem( this );
	}

	public override void Reset()
	{
		_attackFront = _attackRear = _attackRight = _attackLeft = 0f;

		_bitsDamage = 0;

		for ( var i = 0; i < NumDamageTypes; i++ )
			_damage[i].Expire = 0f;
	}

	public override void VidInit()
	{
		_sprite = 0;

		_damageBio = Hud.GetSpriteIndex( "dmg_bio" ) + 1;
		_cross = Hud.GetSpriteIndex( "cross" );

		_damageHeight = Hud.GetSpriteRect( _damageBio ).Right - Hud.GetSpriteRect( _damageBio ).Left;
		_damageWidth = Hud.GetSpriteRect( _damageBio ).Bottom - Hud.GetSpriteRect( _damageBio ).Top;
	}

	private bool MsgFuncHealth( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var x = message.ReadByte();

		Flags |= Active;

		if ( x != Health )
		{
			Fade = Hud.FadeTime;
			Health = x;
		}

		return true;
	}

	private bool MsgFuncDamage( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var armor = message.ReadByte();
		var damageTaken = message.ReadByte();
		var bitsDamage = message.ReadLong();
		var from = new Vector3( message.ReadCoord(), message.ReadCoord(), message.ReadCoord() );

		UpdateTiles( Hud.Time, bitsDamage );

		if ( damageTaken > 0 || armor > 0 )
			CalcDamageDirection( from );

		return true;
	}

	public void GetPainColor( out int r, out int g, out int b )
	{
		if ( Health > 25 )
		{
			Hud.UnpackRgb( out r, out g, out b, Hud.RgbYellowish );
		}
		else
		{
			r = 250;
			g = 0;
			b = 0;
		}
	}

	public override void Draw( float time )
	{
		int a;

		if ( (Hud.HideHudDisplay & Hud.HideHealth) != 0 )
			return;

		if ( _sprite == 0 )
			_sprite = Hud.LoadSprite( "sprites/%d_pain.spr" );

		if ( Fade != 0f )
		{
			Fade -= (float)(Hud.TimeDelta * 20);

			if ( Fade <= 0f )
				Fade = 0f;

			a = (int)(Hud.MinAlpha + (Fade / Hud.FadeTime * 128));
		}
		else
		{
			a = Hud.MinAlpha;
		}

		if ( Health <= 15 )
			a = 255;

		GetPainColor( out var r, out var g, out var b );
		Hud.ScaleColors( ref r, ref g, ref b, a );

		if ( (Hud.WeaponBits & (1 << Hud.WeaponSuit)) != 0 )
		{
			var healthWidth = Hud.GetSpriteRect( Hud.NumberZero ).Right - Hud.GetSpriteRect( Hud.NumberZero ).Left;
			var crossWidth = Hud.GetSpriteRect( _cross ).Right - Hud.GetSpriteRect( _cross ).Left;

			var y = Hud.ScreenHeight - Hud.FontHeight - (Hud.FontHeight / 2);
			var x = crossWidth / 2;

			Engine.SpriteSet( Hud.GetSprite( _cross ), r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y, Hud.GetSpriteRect( _cross ) );

			x = crossWidth + (healthWidth / 2);
			y += (int)(Hud.FontHeight * 0.2f);

			x = Hud.DrawHudNumber( x, y, Hud.Draw3Digits | Hud.DrawZero, Health, r, g, b );

			x += healthWidth / 2;

			Engine.FillRgba( x, y, healthWidth / 10, Hud.FontHeight, 255, 160, 0, a );
		}

		DrawDamage( time );
		DrawPain( time );
	}

	private void CalcDamageDirection( Vector3 from )
	{
		if ( from.x == 0f && from.y == 0f && from.z == 0f )
		{
			_attackFront = _attackRear = _attackRight = _attackLeft = 0f;
			return;
		}

		from -= Hud.Origin;

		var distance = MathLib.Length( from );

		MathLib.VectorNormalize( ref from );
		MathLib.AngleVectors( Hud.Angles, out var forward, out var right, out _ );

		var front = Vector3.Dot( from, right );
		var side = Vector3.Dot( from, forward );

		if ( distance <= 50f )
		{
			_attackFront = _attackRear = _attackRight = _attackLeft = 1f;
			return;
		}

		if ( side > 0f )
		{
			if ( side > 0.3 )
				_attackFront = Math.Max( _attackFront, side );
		}
		else
		{
			var f = Math.Abs( side );

			if ( f > 0.3 )
				_attackRear = Math.Max( _attackRear, f );
		}

		if ( front > 0f )
		{
			if ( front > 0.3 )
				_attackRight = Math.Max( _attackRight, front );
		}
		else
		{
			var f = Math.Abs( front );

			if ( f > 0.3 )
				_attackLeft = Math.Max( _attackLeft, f );
		}
	}

	private void DrawPainDirection( ref float attack, int frame, float fade, Func<int, int, (int X, int Y)> position )
	{
		if ( attack <= 0.4 )
		{
			attack = 0f;
			return;
		}

		GetPainColor( out var r, out var g, out var b );

		var shade = (int)(255 * Math.Max( attack, 0.5f ));

		Hud.ScaleColors( ref r, ref g, ref b, shade );
		Engine.SpriteSet( _sprite, r, g, b );

		var (x, y) = position( Engine.SpriteWidth( _sprite, frame ), Engine.SpriteHeight( _sprite, frame ) );

		Engine.SpriteDrawAdditive( frame, x, y, null );

		attack = Math.Max( 0f, attack - fade );
	}

	private void DrawPain( float time )
	{
		if ( _attackFront == 0f && _attackRear == 0f && _attackLeft == 0f && _attackRight == 0f )
			return;

		var fade = (float)(Hud.TimeDelta * 2);
		var centerX = Hud.ScreenWidth / 2;
		var centerY = Hud.ScreenHeight / 2;

		DrawPainDirection( ref _attackFront, 0, fade, ( width, height ) => (centerX - (width / 2), centerY - (height * 3)) );
		DrawPainDirection( ref _attackRight, 1, fade, ( width, height ) => (centerX + (width * 2), centerY - (height / 2)) );
		DrawPainDirection( ref _attackRear, 2, fade, ( width, height ) => (centerX - (width / 2), centerY + (height * 2)) );
		DrawPainDirection( ref _attackLeft, 3, fade, ( width, height ) => (centerX - (width * 3), centerY - (height / 2)) );
	}

	private void DrawDamage( float time )
	{
		if ( _bitsDamage == 0 )
			return;

		Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );

		var a = (int)(Math.Abs( Math.Sin( time * 2 ) ) * 256.0);

		Hud.ScaleColors( ref r, ref g, ref b, a );

		for ( var i = 0; i < NumDamageTypes; i++ )
		{
			if ( (_bitsDamage & DamageFlags[i]) == 0 )
				continue;

			Engine.SpriteSet( Hud.GetSprite( _damageBio + i ), r, g, b );
			Engine.SpriteDrawAdditive( 0, _damage[i].X, _damage[i].Y, Hud.GetSpriteRect( _damageBio + i ) );
		}

		for ( var i = 0; i < NumDamageTypes; i++ )
		{
			if ( (_bitsDamage & DamageFlags[i]) == 0 )
				continue;

			_damage[i].Expire = Math.Min( time + DamageImageLife, _damage[i].Expire );

			if ( _damage[i].Expire > time || a >= 40 )
				continue;

			_damage[i].Expire = 0f;

			var y = _damage[i].Y;

			_damage[i].X = _damage[i].Y = 0;

			for ( var j = 0; j < NumDamageTypes; j++ )
			{
				if ( _damage[j].Y != 0 && _damage[j].Y < y )
					_damage[j].Y += _damageHeight;
			}

			_bitsDamage &= ~DamageFlags[i];
		}
	}

	private void UpdateTiles( float time, int bitsDamage )
	{
		var bitsOn = ~_bitsDamage & bitsDamage;

		for ( var i = 0; i < NumDamageTypes; i++ )
		{
			if ( (_bitsDamage & DamageFlags[i]) != 0 )
			{
				_damage[i].Expire = time + DamageImageLife;

				if ( _damage[i].Baseline == 0f )
					_damage[i].Baseline = time;
			}

			if ( (bitsOn & DamageFlags[i]) == 0 )
				continue;

			_damage[i].X = _damageWidth / 8;
			_damage[i].Y = Hud.ScreenHeight - (_damageHeight * 2);
			_damage[i].Expire = time + DamageImageLife;

			for ( var j = 0; j < NumDamageTypes; j++ )
			{
				if ( j != i && _damage[j].Y != 0 )
					_damage[j].Y -= _damageHeight;
			}
		}

		_bitsDamage |= bitsDamage;
	}
}
