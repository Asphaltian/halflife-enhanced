namespace HalfLife;

public enum Bullet
{
	None,
	Player9mm,
	PlayerMp5,
	Player357,
	PlayerBuckshot,
	PlayerCrowbar,
	Monster9mm,
	MonsterMp5,
	Monster12mm
}

public enum MonsterState
{
	None,
	Idle,
	Combat,
	Alert,
	Hunt,
	Prone,
	Script,
	PlayDead,
	Dead
}

partial class BaseEntity
{
	public const float DamageNo = 0f;
	public const float DamageYes = 1f;
	public const float DamageAim = 2f;

	public const int DeadNo = 0;
	public const int DeadDying = 1;
	public const int DeadDead = 2;
	public const int DeadRespawnable = 3;
	public const int DeadDiscardBody = 4;

	public const int GibNormal = 0;
	public const int GibNever = 1;
	public const int GibAlways = 2;
	public const float GibHealthValue = -30f;

	public const int DamageGeneric = 0;
	public const int DamageCrush = 1 << 0;
	public const int DamageBullet = 1 << 1;
	public const int DamageSlash = 1 << 2;
	public const int DamageBurn = 1 << 3;
	public const int DamageFreeze = 1 << 4;
	public const int DamageFall = 1 << 5;
	public const int DamageBlast = 1 << 6;
	public const int DamageClub = 1 << 7;
	public const int DamageShock = 1 << 8;
	public const int DamageSonic = 1 << 9;
	public const int DamageEnergyBeam = 1 << 10;
	public const int DamageNeverGib = 1 << 12;
	public const int DamageAlwaysGib = 1 << 13;
	public const int DamageDrown = 1 << 14;
	public const int DamageTimeBased = ~0x3fff;
	public const int DamageParalyze = 1 << 15;
	public const int DamageNerveGas = 1 << 16;
	public const int DamagePoison = 1 << 17;
	public const int DamageRadiation = 1 << 18;
	public const int DamageDrownRecover = 1 << 19;
	public const int DamageAcid = 1 << 20;
	public const int DamageSlowBurn = 1 << 21;
	public const int DamageSlowFreeze = 1 << 22;
	public const int DamageMortar = 1 << 23;
	public const int DamageGibCorpse = DamageCrush | DamageFall | DamageBlast | DamageSonic | DamageClub;
	public const int DamageShownHud = DamagePoison | DamageAcid | DamageFreeze | DamageSlowFreeze | DamageDrown | DamageBurn | DamageSlowBurn | DamageNerveGas | DamageRadiation | DamageShock;
	public const int DamageNoPhysicsForce = DamageFall | DamageBurn | DamageDrown | DamageTimeBased | DamageCrush;

	public const int ClassNone = 0;
	public const int ClassMachine = 1;
	public const int ClassPlayer = 2;
	public const int ClassHumanPassive = 3;
	public const int ClassHumanMilitary = 4;
	public const int ClassAlienMilitary = 5;
	public const int ClassAlienPassive = 6;
	public const int ClassAlienMonster = 7;
	public const int ClassAlienPrey = 8;
	public const int ClassAlienPredator = 9;
	public const int ClassInsect = 10;
	public const int ClassPlayerAlly = 11;
	public const int ClassPlayerBioWeapon = 12;
	public const int ClassAlienBioWeapon = 13;
	public const int ClassVehicle = 14;
	public const int ClassBarnacle = 99;

	public const int DontBleed = -1;
	public const int BloodColorRed = 247;
	public const int BloodColorYellow = 195;
	public const int BloodColorGreen = BloodColorYellow;

	public const int HitGroupGeneric = 0;
	public const int HitGroupHead = 1;
	public const int HitGroupChest = 2;
	public const int HitGroupStomach = 3;
	public const int HitGroupLeftArm = 4;
	public const int HitGroupRightArm = 5;
	public const int HitGroupLeftLeg = 6;
	public const int HitGroupRightLeg = 7;

	public static readonly Vector3 ViewVector = new( 0f, 0f, 28f );
	public static readonly Vector3 HumanHullMin = new( -16f, -16f, 0f );
	public static readonly Vector3 HumanHullMax = new( 16f, 16f, 72f );

	public const int IgnoreMonsters = 1;
	public const int DontIgnoreMonsters = 0;
	public const int Missile = 2;
	public const int IgnoreGlass = 0x100;

	public const int PointHull = 0;
	public const int HumanHull = 1;
	public const int LargeHull = 2;
	public const int HeadHull = 3;
}
