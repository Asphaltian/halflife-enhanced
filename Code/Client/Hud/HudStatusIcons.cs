using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudStatusIcons( Hud hud ) : HudBase( hud )
{
	private const int MaxIconSprites = 4;

	private struct IconSprite
	{
		public string SpriteName;
		public int Sprite;
		public SpriteRect Rect;
		public int R;
		public int G;
		public int B;
	}

	private readonly IconSprite[] _iconList = new IconSprite[MaxIconSprites];

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "StatusIcon", MsgFuncStatusIcon );

		Hud.AddHudElem( this );

		Reset();
	}

	public override void Reset()
	{
		Array.Clear( _iconList );
		Flags &= ~Active;
	}

	public override void Draw( float time )
	{
		var x = 5;
		var y = Hud.ScreenHeight / 2;

		foreach ( var icon in _iconList )
		{
			if ( icon.Sprite == 0 )
				continue;

			y -= icon.Rect.Bottom - icon.Rect.Top + 5;

			Engine.SpriteSet( icon.Sprite, icon.R, icon.G, icon.B );
			Engine.SpriteDrawAdditive( 0, x, y, icon.Rect );
		}
	}

	private bool MsgFuncStatusIcon( string name, byte[] data )
	{
		var message = new MessageBuffer( data );
		var shouldEnable = message.ReadByte();
		var iconName = message.ReadString();

		if ( shouldEnable != 0 )
		{
			var r = message.ReadByte();
			var g = message.ReadByte();
			var b = message.ReadByte();

			EnableIcon( iconName, r, g, b );
			Flags |= Active;
		}
		else
		{
			DisableIcon( iconName );
		}

		return true;
	}

	private int FindIcon( string iconName )
	{
		return Array.FindIndex( _iconList, x => string.Equals( x.SpriteName ?? "", iconName, StringComparison.OrdinalIgnoreCase ) );
	}

	public void EnableIcon( string iconName, int red, int green, int blue )
	{
		var i = FindIcon( iconName );

		if ( i < 0 )
			i = Array.FindIndex( _iconList, x => x.Sprite == 0 );

		if ( i < 0 )
			i = 0;

		var spriteIndex = Hud.GetSpriteIndex( iconName );

		_iconList[i] = new IconSprite
		{
			SpriteName = iconName,
			Sprite = Hud.GetSprite( spriteIndex ),
			Rect = spriteIndex < 0 ? default : Hud.GetSpriteRect( spriteIndex ),
			R = red,
			G = green,
			B = blue
		};

		if ( iconName.Contains( "grenade", StringComparison.Ordinal ) )
		{
			var player = Engine.GetLocalPlayer();

			Engine.PlaySound( player.Index, player.Origin, SoundChannel.Static, "weapons/timer.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
		}
	}

	public void DisableIcon( string iconName )
	{
		var i = FindIcon( iconName );

		if ( i >= 0 )
			_iconList[i] = default;
	}
}
