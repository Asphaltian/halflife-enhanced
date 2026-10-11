using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monster_generic" )]
public class GenericMonster : BaseMonster
{
	private const int SpawnFlagNotSolid = 4;

	public override int Classify() => ClassPlayerAlly;

	public override void SetYawSpeed() => YawSpeed = 90f;

	public override int SoundMask() => 0;

	public override void Spawn()
	{
		Precache();

		SetModel( ModelName );

		if ( ModelName is "models/player.mdl" or "models/holo.mdl" )
			SetSize( Player.HullMin, Player.HullMax );
		else
			SetSize( HumanHullMin, HumanHullMax );

		Solid = Solid.SlideBox;
		MoveType = MoveType.Step;
		MonsterBloodColor = BloodColorRed;
		Health = 8f;
		FieldOfView = 0.5f;
		MonsterState = MonsterState.None;

		MonsterInit();

		if ( (SpawnFlags & SpawnFlagNotSolid) != 0 )
		{
			Solid = Solid.Not;
			DamageMode = DamageNo;
		}
	}

	public override void Precache() => Server.PrecacheModel( ModelName );
}
