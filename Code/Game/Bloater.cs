using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_bloater" )]
public class Bloater : BaseMonster
{
	private const int EventAttackMelee1 = 0x01;

	public override int Classify() => ClassAlienMonster;

	public override void SetYawSpeed() => YawSpeed = 120f;

	public override bool CheckRangeAttack1( float dot, float distance ) => false;

	public override bool CheckRangeAttack2( float dot, float distance ) => false;

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		if ( monsterEvent.Event != EventAttackMelee1 )
			base.HandleAnimEvent( monsterEvent );
	}

	public override void Spawn()
	{
		Precache();

		SetModel( "models/floater.mdl" );
		SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Fly;
		SpawnFlags |= (int)EntityFlags.Fly;
		MonsterBloodColor = BloodColorGreen;
		Health = 40f;
		ViewOffset = ViewVector;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/floater.mdl" );
	}
}
