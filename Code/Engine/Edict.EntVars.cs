namespace GoldSrc;

partial class Edict
{
	[Save( FieldType.String )]
	public string ClassName { get; set; }
	[Save( FieldType.String, Global = true )]
	public string GlobalName { get; set; }

	[Save( FieldType.PositionVector )]
	public Vector3 Origin
	{
		get => _origin;
		set
		{
			_origin = value;

			_ownTransform = true;
			WorldPosition = value;
			_ownTransform = false;
		}
	}

	[Save( FieldType.PositionVector )]
	public Vector3 OldOrigin { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 Velocity { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 BaseVelocity { get; set; }
	public Vector3 ClBaseVelocity { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 MoveDir { get; set; }

	[Save( FieldType.Vector )]
	public Vector3 Angles
	{
		get => _angles;
		set
		{
			_angles = value;

			_ownTransform = true;
			WorldRotation = ToRotation( value );
			_ownTransform = false;
		}
	}

	[Save( FieldType.Vector )]
	public Vector3 AVelocity { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 PunchAngle { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 ViewAngle { get; set; }

	[Save( FieldType.Float )]
	public int FixAngle { get; set; }
	[Save( FieldType.Float )]
	public float IdealPitch { get; set; }
	[Save( FieldType.Float )]
	public float PitchSpeed { get; set; }
	[Save( FieldType.Float )]
	public float IdealYaw { get; set; }
	[Save( FieldType.Float )]
	public float YawSpeed { get; set; }

	[Save( FieldType.Integer )]
	public int ModelIndex { get; set; }
	[Save( FieldType.ModelName, Global = true )]
	public string ModelName { get; set; }
	[Save( FieldType.ModelName )]
	public string ViewModel { get; set; }
	[Save( FieldType.ModelName )]
	public string WeaponModel { get; set; }

	[Save( FieldType.PositionVector )]
	public Vector3 AbsMin { get; set; }
	[Save( FieldType.PositionVector )]
	public Vector3 AbsMax { get; set; }
	[Save( FieldType.Vector, Global = true )]
	public Vector3 Mins { get; set; }
	[Save( FieldType.Vector, Global = true )]
	public Vector3 Maxs { get; set; }
	[Save( FieldType.Vector, Global = true )]
	public Vector3 Size { get; set; }

	[Save( FieldType.Time )]
	public float LTime { get; set; }
	[Save( FieldType.Time )]
	public float NextThink { get; set; }

	[Save( FieldType.Integer )]
	public MoveType MoveType { get; set; }
	[Save( FieldType.Integer )]
	public Solid Solid { get; set; }

	[Save( FieldType.Integer )]
	public int GaitSequence { get; set; }
	[Save( FieldType.Integer )]
	public int WeaponAnim { get; set; }
	[Save( FieldType.Float )]
	public float Fov { get; set; }

	[Save( FieldType.Integer )]
	public int Skin { get; set; }
	[Save( FieldType.Integer )]
	public int Body { get; set; }
	[Save( FieldType.Integer )]
	public Effects Effects { get; set; }

	[Save( FieldType.Float )]
	public float Gravity { get; set; }
	[Save( FieldType.Float )]
	public float Friction { get; set; }
	[Save( FieldType.Float )]
	public int LightLevel { get; set; }

	[Save( FieldType.Integer )]
	public int Sequence { get; set; }
	[Save( FieldType.Float )]
	public float Frame { get; set; }
	[Save( FieldType.Time )]
	public float AnimTime { get; set; }
	[Save( FieldType.Float )]
	public float FrameRate { get; set; }
	[Save( FieldType.Integer )]
	public byte[] Controller { get; } = new byte[4];
	[Save( FieldType.Integer )]
	public byte[] Blending { get; } = new byte[2];
	[Save( FieldType.Float )]
	public float Scale { get; set; }

	[Save( FieldType.Integer )]
	public RenderMode RenderMode { get; set; }
	[Save( FieldType.Float )]
	public float RenderAmt { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 RenderColor { get; set; }
	[Save( FieldType.Integer )]
	public RenderFx RenderFx { get; set; }

	[Save( FieldType.Float )]
	public float Health { get; set; }
	[Save( FieldType.Float )]
	public float Frags { get; set; }
	[Save( FieldType.Integer )]
	public int Weapons { get; set; }
	[Save( FieldType.Float )]
	public float DamageMode { get; set; }

	[Save( FieldType.Float )]
	public int DeadFlag { get; set; }
	[Save( FieldType.Vector )]
	public Vector3 ViewOffset { get; set; }
	[Save( FieldType.Integer )]
	public int Button { get; set; }
	[Save( FieldType.Integer )]
	public int Impulse { get; set; }
	public int PlayerClass { get; set; }

	public Vector3 ParametricStartPos { get; set; }
	public Vector3 ParametricEndPos { get; set; }
	public float ImpactTime { get; set; }
	public float ParametricStartTime { get; set; }

	[Save( FieldType.Edict )]
	public Edict Chain { get; set; }
	[Save( FieldType.Edict )]
	public Edict DamageInflictor { get; set; }
	[Save( FieldType.Edict )]
	public Edict Enemy { get; set; }
	[Save( FieldType.Edict )]
	public Edict AimEntity { get; set; }
	[Save( FieldType.Edict )]
	public Edict Owner { get; set; }
	[Save( FieldType.Edict )]
	public Edict GroundEntity { get; set; }

	[Save( FieldType.Integer )]
	public int SpawnFlags { get; set; }
	[Save( FieldType.Float )]
	public EntityFlags EntityFlags { get; set; }

	[Save( FieldType.Integer )]
	public int Colormap { get; set; }
	[Save( FieldType.Integer )]
	public int Team { get; set; }

	[Save( FieldType.Float )]
	public float MaxHealth { get; set; }
	[Save( FieldType.Time )]
	public float TeleportTime { get; set; }
	[Save( FieldType.Float )]
	public float ArmorType { get; set; }
	[Save( FieldType.Float )]
	public float ArmorValue { get; set; }
	[Save( FieldType.Integer )]
	public int WaterLevel { get; set; }
	[Save( FieldType.Integer )]
	public int WaterType { get; set; }

	[Save( FieldType.String, Global = true )]
	public string Target { get; set; }
	[Save( FieldType.String, Global = true )]
	public string TargetName { get; set; }
	[Save( FieldType.String )]
	public string NetName { get; set; }
	[Save( FieldType.String )]
	public string Message { get; set; }

	[Save( FieldType.Float )]
	public float DamageTake { get; set; }
	[Save( FieldType.Float )]
	public float DamageSave { get; set; }
	[Save( FieldType.Float )]
	public float Damage { get; set; }
	[Save( FieldType.Time )]
	public float DamageTime { get; set; }

	[Save( FieldType.SoundName )]
	public string Noise { get; set; }
	[Save( FieldType.SoundName )]
	public string Noise1 { get; set; }
	[Save( FieldType.SoundName )]
	public string Noise2 { get; set; }
	[Save( FieldType.SoundName )]
	public string Noise3 { get; set; }

	[Save( FieldType.Float )]
	public float Speed { get; set; }
	[Save( FieldType.Time )]
	public float AirFinished { get; set; }
	[Save( FieldType.Time )]
	public float PainFinished { get; set; }
	[Save( FieldType.Time )]
	public float RadSuitFinished { get; set; }

	public float MaxSpeed { get; set; }
	public int PushMsec { get; set; }
	public bool InDuck { get; set; }
	public int TimeStepSound { get; set; }
	public int SwimTime { get; set; }
	public int DuckTime { get; set; }
	public int StepLeft { get; set; }
	public float FallVelocity { get; set; }
	public int OldButtons { get; set; }

	public int IUser1 { get; set; }
	public int IUser2 { get; set; }
	public int IUser3 { get; set; }
	public int IUser4 { get; set; }
	public float FUser1 { get; set; }
	public float FUser2 { get; set; }
	public float FUser3 { get; set; }
	public float FUser4 { get; set; }
	public Vector3 VUser1 { get; set; }
	public Vector3 VUser2 { get; set; }
	public Vector3 VUser3 { get; set; }
	public Vector3 VUser4 { get; set; }

	private Vector3 _origin;
	private Vector3 _angles;
}
