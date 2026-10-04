namespace HalfLife;

public sealed class HudGeiger( Hud hud ) : HudBase( hud )
{
	private int _range;

	public override void Init()
	{
		Hud.Dll.HookUserMsg( "Geiger", MsgFuncGeiger );

		_range = 0;
		Flags = 0;

		Hud.AddHudElem( this );
	}

	private bool MsgFuncGeiger( string name, byte[] data )
	{
		var message = new MessageBuffer( data );

		_range = message.ReadByte();
		_range <<= 2;

		Flags |= Active;

		return true;
	}

	public override void Draw( float time )
	{
		if ( _range is >= 1000 or <= 0 )
			return;

		int percent;
		var volume = 0f;
		var count = 0;

		if ( _range > 800 )
		{
			percent = 0;
		}
		else if ( _range > 600 )
		{
			percent = 2;
			volume = 0.4f;
			count = 2;
		}
		else if ( _range > 500 )
		{
			percent = 4;
			volume = 0.5f;
			count = 2;
		}
		else if ( _range > 400 )
		{
			percent = 8;
			volume = 0.6f;
			count = 3;
		}
		else if ( _range > 300 )
		{
			percent = 8;
			volume = 0.7f;
			count = 3;
		}
		else if ( _range > 200 )
		{
			percent = 28;
			volume = 0.78f;
			count = 3;
		}
		else if ( _range > 150 )
		{
			percent = 40;
			volume = 0.80f;
			count = 3;
		}
		else if ( _range > 100 )
		{
			percent = 60;
			volume = 0.85f;
			count = 3;
		}
		else if ( _range > 75 )
		{
			percent = 80;
			volume = 0.9f;
			count = 3;
		}
		else if ( _range > 50 )
		{
			percent = 90;
			volume = 0.95f;
			count = 2;
		}
		else
		{
			percent = 95;
			volume = 1f;
			count = 2;
		}

		volume = (volume * (Hud.Rand() & 127) / 255) + 0.25f;

		if ( (Hud.Rand() & 127) >= percent && (Hud.Rand() & 127) >= percent )
			return;

		var j = Hud.Rand() & 1;

		if ( count > 2 )
			j += Hud.Rand() & 1;

		Engine.PlaySoundByName( $"player/geiger{j + 1}.wav", volume );
	}
}
