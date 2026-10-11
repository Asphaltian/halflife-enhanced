using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_rat" )]
public class Rat : BaseMonster
{
	public override int Classify() => ClassInsect;

	public override void SetYawSpeed() => YawSpeed = 45f;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/bigrat.mdl" );
		SetSize( Vector3.Zero, Vector3.Zero );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Health = 8f;
		ViewOffset = new Vector3( 0f, 0f, 6f );
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/bigrat.mdl" );
	}
}
