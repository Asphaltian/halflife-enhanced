namespace HalfLife;

public sealed class HudTrain( Hud hud ) : HudBase( hud )
{
	private int _sprite;
	private int _position;

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "Train", MsgFuncTrain );

		_position = 0;
		Flags = 0;

		Hud.AddHudElem( this );
	}

	public override void VidInit()
	{
		_sprite = 0;
	}

	public override void Draw( float time )
	{
		if ( _sprite == 0 )
			_sprite = Hud.LoadSprite( "sprites/%d_train.spr" );

		if ( _position == 0 )
			return;

		Hud.UnpackRgb( out var r, out var g, out var b, Hud.RgbYellowish );
		Engine.SpriteSet( _sprite, r, g, b );

		var y = Hud.ScreenHeight - Engine.SpriteHeight( _sprite, 0 ) - Hud.FontHeight;
		var x = (Hud.ScreenWidth / 3) + (Engine.SpriteWidth( _sprite, 0 ) / 4);

		Engine.SpriteDrawAdditive( _position - 1, x, y, null );
	}

	private bool MsgFuncTrain( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_position = message.ReadByte();

		if ( _position != 0 )
			Flags |= Active;
		else
			Flags &= ~Active;

		return true;
	}
}
