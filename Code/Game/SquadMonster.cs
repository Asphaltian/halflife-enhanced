using GoldSrc;

namespace HalfLife;

public static class SquadSlot
{
	public const int None = 0;

	public const int HumanGruntEngage1 = 1 << 0;
	public const int HumanGruntEngage2 = 1 << 1;
	public const int HumanGruntEngage = HumanGruntEngage1 | HumanGruntEngage2;

	public const int HumanGruntGrenade1 = 1 << 2;
	public const int HumanGruntGrenade2 = 1 << 3;
	public const int HumanGruntGrenade = HumanGruntGrenade1 | HumanGruntGrenade2;

	public const int AlienGruntHornet1 = 1 << 4;
	public const int AlienGruntHornet2 = 1 << 5;
	public const int AlienGruntChase = 1 << 6;
	public const int AlienGruntHornet = AlienGruntHornet1 | AlienGruntHornet2;

	public const int HoundAttack1 = 1 << 7;
	public const int HoundAttack2 = 1 << 8;
	public const int HoundAttack3 = 1 << 9;
	public const int HoundAttack = HoundAttack1 | HoundAttack2 | HoundAttack3;

	public const int SquadSplit = 1 << 10;

	public const int Count = 11;
}

public abstract class SquadMonster : BaseMonster
{
	public const int SpawnFlagLeader = 32;
	public const int MaxSquadMembers = 5;

	[Save( FieldType.EHandle )]
	private BaseEntity _squadLeader;
	[Save( FieldType.EHandle )]
	private readonly BaseEntity[] _squadMember = new BaseEntity[MaxSquadMembers - 1];

	public int SquadSlots;
	[Save( FieldType.Boolean )]
	public bool EnemyEluded;
	[Save( FieldType.Time )]
	public float LastEnemySightTime;
	[Save( FieldType.Integer )]
	public int MySlot;

	private readonly struct Plane( Vector3 normal, Vector3 point )
	{
		private readonly Vector3 _normal = normal;
		private readonly float _distance = Vector3.Dot( normal, point );

		public bool PointInFront( Vector3 point ) => Vector3.Dot( _normal, point ) - _distance >= 0f;
	}

	public SquadMonster SquadLeader => _squadLeader.IsValid() ? (SquadMonster)_squadLeader : null;

	public SquadMonster MySquadLeader() => SquadLeader ?? this;

	public SquadMonster MySquadMember( int i )
	{
		if ( i >= MaxSquadMembers - 1 )
			return this;

		return _squadMember[i].IsValid() ? (SquadMonster)_squadMember[i] : null;
	}

	public bool InSquad => SquadLeader is not null;

	public bool IsLeader => SquadLeader == this;

	public bool OccupySlot( int desiredSlots )
	{
		if ( !InSquad )
			return true;

		if ( SquadEnemySplit() )
		{
			MySlot = SquadSlot.SquadSplit;
			return true;
		}

		var squadLeader = MySquadLeader();

		if ( (desiredSlots ^ squadLeader.SquadSlots) == 0 )
			return false;

		var squadSlots = squadLeader.SquadSlots;

		for ( var i = 0; i < SquadSlot.Count; i++ )
		{
			var mask = 1 << i;

			if ( (desiredSlots & mask) == 0 || (squadSlots & mask) != 0 )
				continue;

			squadLeader.SquadSlots |= mask;
			MySlot = mask;

			return true;
		}

		return false;
	}

	public void VacateSlot()
	{
		if ( MySlot == SquadSlot.None || !InSquad )
			return;

		MySquadLeader().SquadSlots &= ~MySlot;
		MySlot = SquadSlot.None;
	}

	public override void ScheduleChange() => VacateSlot();

	public override void Killed( BaseEntity attacker, int gib )
	{
		VacateSlot();

		if ( InSquad )
			MySquadLeader().SquadRemove( this );

		base.Killed( attacker, gib );
	}

	public void SquadRemove( SquadMonster remove )
	{
		if ( remove == MySquadLeader() )
		{
			for ( var i = 0; i < MaxSquadMembers - 1; i++ )
			{
				if ( MySquadMember( i ) is not { } member )
					continue;

				member._squadLeader = null;
				_squadMember[i] = null;
			}
		}
		else
		{
			var squadLeader = MySquadLeader();

			for ( var i = 0; i < MaxSquadMembers - 1; i++ )
			{
				if ( squadLeader._squadMember[i] != this )
					continue;

				squadLeader._squadMember[i] = null;
				break;
			}
		}

		remove._squadLeader = null;
	}

	public bool SquadAdd( SquadMonster add )
	{
		for ( var i = 0; i < MaxSquadMembers - 1; i++ )
		{
			if ( _squadMember[i].IsValid() )
				continue;

			_squadMember[i] = add;
			add._squadLeader = this;

			return true;
		}

		return false;
	}

	public void SquadPasteEnemyInfo() => MySquadLeader().EnemyLastKnownPosition = EnemyLastKnownPosition;

	public void SquadCopyEnemyInfo() => EnemyLastKnownPosition = MySquadLeader().EnemyLastKnownPosition;

	public void SquadMakeEnemy( BaseEntity enemy )
	{
		if ( !InSquad )
			return;

		if ( enemy is null )
		{
			Util.Alert( AlertType.Console, "ERROR: SquadMakeEnemy() - pEnemy is NULL!" );
			return;
		}

		var squadLeader = MySquadLeader();

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( squadLeader.MySquadMember( i ) is not { } member )
				continue;

			if ( member.EnemyEntity == enemy || member.HasConditions( ConditionSeeEnemy ) )
				continue;

			if ( member.EnemyEntity is { } current )
				member.PushEnemy( current, member.EnemyLastKnownPosition );

			member.EnemyEntity = enemy;
			member.EnemyLastKnownPosition = enemy.Origin;
			member.SetConditions( ConditionNewEnemy );
		}
	}

	public int SquadCount()
	{
		if ( !InSquad )
			return 0;

		var squadLeader = MySquadLeader();
		var count = 0;

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( squadLeader.MySquadMember( i ) is not null )
				count++;
		}

		return count;
	}

	public int SquadRecruit( int searchRadius, int maxMembers )
	{
		var myClass = Classify();

		if ( InSquad )
			return 0;

		if ( maxMembers < 2 )
			return 0;

		_squadLeader = this;

		var count = 1;
		BaseEntity entity = null;

		if ( !string.IsNullOrEmpty( NetName ) )
		{
			while ( (entity = Util.FindEntityByString( entity, "netname", NetName )) is not null )
			{
				if ( entity is not SquadMonster recruit )
					continue;

				if ( recruit.InSquad || recruit.Classify() != myClass || recruit == this )
					continue;

				if ( !SquadAdd( recruit ) )
					break;

				count++;
			}
		}
		else
		{
			while ( (entity = Util.FindEntityInSphere( entity, Origin, searchRadius )) is not null )
			{
				if ( entity is not SquadMonster recruit || recruit == this || !recruit.IsAlive || recruit.Cine is not null )
					continue;

				if ( recruit.InSquad || recruit.Classify() != myClass
					|| (myClass == ClassAlienMonster && ClassName != recruit.ClassName)
					|| !string.IsNullOrEmpty( recruit.NetName ) )
				{
					continue;
				}

				var trace = Util.TraceLine( Origin + ViewOffset, recruit.Origin + ViewOffset, IgnoreMonsters, recruit );

				if ( trace.Fraction != 1f )
					continue;

				if ( !SquadAdd( recruit ) )
					break;

				count++;
			}
		}

		if ( count == 1 )
			_squadLeader = null;

		return count;
	}

	public override bool CheckEnemy( BaseEntity enemy )
	{
		var updatedLastKnownPosition = base.CheckEnemy( EnemyEntity );

		if ( InSquad && EnemyEntity == MySquadLeader().EnemyEntity )
		{
			if ( updatedLastKnownPosition )
				SquadPasteEnemyInfo();
			else
				SquadCopyEnemyInfo();
		}

		return updatedLastKnownPosition;
	}

	public override void StartMonster()
	{
		base.StartMonster();

		if ( (Capability & CapabilitySquad) == 0 || InSquad )
			return;

		if ( !string.IsNullOrEmpty( NetName ) && (SpawnFlags & SpawnFlagLeader) == 0 )
			return;

		var squadSize = SquadRecruit( 1024, 4 );

		if ( squadSize != 0 )
			Util.Alert( AlertType.AiConsole, $"Squad of {squadSize} {ClassName} formed" );

		if ( IsLeader && ClassName == "monster_human_grunt" )
		{
			SetBodygroup( 1, 1 );
			Skin = 0;
		}
	}

	public bool NoFriendlyFire()
	{
		if ( !InSquad )
			return true;

		if ( EnemyEntity is not { } enemy )
			return false;

		Server.Globals.MakeVectors( MathLib.VectorAngles( enemy.Center - Origin ) );

		var right = Server.Globals.Right;
		var leftPlane = new Plane( right, Origin - (right * (Size.x * 1.5f)) );
		var rightPlane = new Plane( right * -1f, Origin + (right * (Size.x * 1.5f)) );
		var backPlane = new Plane( Server.Globals.Forward, Origin );

		var squadLeader = MySquadLeader();

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( squadLeader.MySquadMember( i ) is not { } member || member == this )
				continue;

			if ( backPlane.PointInFront( member.Origin ) && leftPlane.PointInFront( member.Origin ) && rightPlane.PointInFront( member.Origin ) )
				return false;
		}

		return true;
	}

	public override MonsterState GetIdealState()
	{
		if ( MonsterState is MonsterState.Idle or MonsterState.Alert && HasConditions( ConditionNewEnemy ) && InSquad )
			SquadMakeEnemy( EnemyEntity );

		return base.GetIdealState();
	}

	public override bool ValidateCover( Vector3 coverLocation ) => !InSquad || !SquadMemberInRange( coverLocation, 128f );

	public bool SquadEnemySplit()
	{
		if ( !InSquad )
			return false;

		var squadLeader = MySquadLeader();
		var enemy = squadLeader.EnemyEntity;

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( squadLeader.MySquadMember( i ) is { EnemyEntity: { } memberEnemy } && memberEnemy != enemy )
				return true;
		}

		return false;
	}

	public bool SquadMemberInRange( Vector3 location, float distance )
	{
		if ( !InSquad )
			return false;

		var squadLeader = MySquadLeader();

		for ( var i = 0; i < MaxSquadMembers; i++ )
		{
			if ( squadLeader.MySquadMember( i ) is { } member && MathLib.Length2D( location - member.Origin ) <= distance )
				return true;
		}

		return false;
	}

	protected Schedule MonsterScheduleOfType( int type ) => base.GetScheduleOfType( type );

	public override Schedule GetScheduleOfType( int type )
	{
		return type == ScheduleType.ChaseEnemyFailed ? DefaultAi.ChaseEnemyFailed : base.GetScheduleOfType( type );
	}
}
