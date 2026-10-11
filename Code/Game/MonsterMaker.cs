using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "monstermaker" )]
public class MonsterMaker : BaseMonster
{
	private const int SpawnFlagStartOn = 1;
	private const int SpawnFlagCyclic = 4;
	private const int SpawnFlagMonsterClip = 8;

	[Save( FieldType.String )]
	private string _monsterClassName;
	[Save( FieldType.Integer )]
	private int _monsterCount;
	[Save( FieldType.Integer )]
	private int _liveChildren;
	[Save( FieldType.Float )]
	private float _ground;
	[Save( FieldType.Integer )]
	private int _maxLiveChildren;
	[Save( FieldType.Boolean )]
	private bool _active;
	[Save( FieldType.Boolean )]
	private bool _fadeChildren;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "monstercount":
				_monsterCount = Util.Atoi( value );
				return true;
			case "m_imaxlivechildren":
				_maxLiveChildren = Util.Atoi( value );
				return true;
			case "monstertype":
				_monsterClassName = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Spawn()
	{
		Solid = Solid.Not;

		_liveChildren = 0;

		Precache();

		if ( !string.IsNullOrEmpty( TargetName ) )
		{
			SetUse( (SpawnFlags & SpawnFlagCyclic) != 0 ? CyclicUse : ToggleUse );

			if ( (SpawnFlags & SpawnFlagStartOn) != 0 )
			{
				_active = true;
				SetThink( MakerThink );
			}
			else
			{
				_active = false;
				SetThink( SubDoNothing );
			}
		}
		else
		{
			NextThink = Server.Globals.Time + Delay;
			_active = true;
			SetThink( MakerThink );
		}

		_fadeChildren = _monsterCount != 1;
		_ground = 0f;
	}

	public override void Precache()
	{
		base.Precache();

		Util.PrecacheOther( _monsterClassName );
	}

	public void MakeMonster()
	{
		if ( _maxLiveChildren > 0 && _liveChildren >= _maxLiveChildren )
			return;

		if ( _ground == 0f )
			_ground = Util.TraceLine( Origin, Origin - new Vector3( 0f, 0f, 2048f ), IgnoreMonsters, this ).EndPos.z;

		var mins = new Vector3( Origin.x - 34f, Origin.y - 34f, _ground );
		var maxs = new Vector3( Origin.x + 34f, Origin.y + 34f, Origin.z );

		if ( Util.EntitiesInBox( new BaseEntity[2], mins, maxs, EntityFlags.Client | EntityFlags.Monster ) != 0 )
			return;

		var child = Server.CreateNamedEntity( _monsterClassName );

		if ( Util.IsNullEnt( child ) )
		{
			Util.Alert( AlertType.Console, "NULL Ent in MonsterMaker!\n" );
			return;
		}

		if ( !string.IsNullOrEmpty( Target ) )
			FireTargets( Target, this, this, UseType.Toggle, 0f );

		child.Origin = Origin;
		child.Angles = Angles;
		child.SpawnFlags |= SpawnFlagFallToGround;

		if ( (SpawnFlags & SpawnFlagMonsterClip) != 0 )
			child.SpawnFlags |= SpawnFlagHitMonsterClip;

		child.DispatchSpawn();
		child.Owner = this;

		if ( !string.IsNullOrEmpty( NetName ) )
			child.TargetName = NetName;

		_liveChildren++;
		_monsterCount--;

		if ( _monsterCount == 0 )
		{
			SetThink( null );
			SetUse( null );
		}
	}

	public void CyclicUse( BaseEntity activator, BaseEntity caller, UseType useType, float value ) => MakeMonster();

	public void ToggleUse( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, _active ) )
			return;

		if ( _active )
		{
			_active = false;
			SetThink( null );
		}
		else
		{
			_active = true;
			SetThink( MakerThink );
		}

		NextThink = Server.Globals.Time;
	}

	public void MakerThink()
	{
		NextThink = Server.Globals.Time + Delay;

		MakeMonster();
	}

	public override void DeathNotice( BaseEntity child )
	{
		_liveChildren--;

		if ( !_fadeChildren )
			child.Owner = null;
	}
}
