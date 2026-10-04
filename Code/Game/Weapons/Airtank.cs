using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "item_airtank" )]
public class Airtank : Grenade
{
	[Save( FieldType.Integer )]
	private int _state;

	public override void Spawn()
	{
		Precache();

		MoveType = MoveType.Fly;
		Solid = Solid.BBox;

		SetModel( "models/w_oxygen.mdl" );
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 36f ) );
		SetOrigin( Origin );

		SetTouch( TankTouch );
		SetThink( TankThink );

		EntityFlags |= EntityFlags.Monster;
		DamageMode = DamageYes;
		Health = 20f;
		Damage = 50f;
		_state = 1;
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/w_oxygen.mdl" );
		Server.PrecacheSound( "doors/aliendoor3.wav" );
	}

	public override void Killed( BaseEntity attacker, int gib )
	{
		Owner = attacker;

		Explode( Origin, new Vector3( 0f, 0f, -1f ) );
	}

	public void TankThink()
	{
		_state = 1;
		UseTargets( this, UseType.Toggle, 0f );
	}

	public void TankTouch( BaseEntity other )
	{
		if ( !other.IsPlayer )
			return;

		if ( _state == 0 )
		{
			Sound.Emit( this, SoundChannel.Body, "player/pl_swim2.wav", 1f, Server.AttenuationNormal );
			return;
		}

		other.AirFinished = Server.Globals.Time + 12f;

		Sound.Emit( this, SoundChannel.Voice, "doors/aliendoor3.wav", 1f, Server.AttenuationNormal );

		NextThink = Server.Globals.Time + 30f;
		_state = 0;
		UseTargets( this, UseType.Toggle, 1f );
	}
}
