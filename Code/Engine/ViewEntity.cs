using GoldSrc.Studio;

namespace GoldSrc;

public sealed class ViewEntity : Component
{
	[ConVar( "r_drawviewmodel" )]
	public static float DrawViewModel { get; set; } = 1f;

	[ConVar( "r_drawentities" )]
	public static float DrawEntities { get; set; } = 1f;

	private const float ViewModelDepthRange = 0.3f;

	[Property] public Vector3 Origin;
	[Property] public Vector3 Angles;
	[Property] public Effects Effects;
	[Property] public int Body;
	[Property] public int WeaponSequence;
	[Property] public double WeaponStartTime;
	[Property] public bool ResentEveryCommand;
	[Property] public bool NoModel;
	[Property] public int ModelIndex;
	[Property] public int Index;

	public Vector3[] Attachments { get; } = new Vector3[4];

	private readonly BoneOutput _boneOutput = new();
	private SkinnedModelRenderer _renderer;
	private Sandbox.Model _renderModel;
	private EntityState _state;
	private LatchedVars _latched = new() { PrevController = new byte[4] };

	protected override void OnPreRender()
	{
		_boneOutput.UpdateBounds( _renderer );
	}

	public void ClearState()
	{
		Effects = 0;
		Body = 0;
		WeaponSequence = 0;
		WeaponStartTime = 0.0;
		ResentEveryCommand = false;

		_state = new EntityState();
		_latched = new LatchedVars { PrevController = new byte[4] };
	}

	public void WeaponAnim( int sequence, int body )
	{
		WeaponStartTime = 0.0;
		WeaponSequence = sequence;
		Body = body;
	}

	public void Draw( Client client, int health )
	{
		var modelIndex = ModelIndex;
		var playerIndex = Index;

		var header = client.GetStudioModel( modelIndex );

		var visible = DrawViewModel != 0f
			&& !client.IsThirdPerson
			&& Client.ChaseActive == 0f
			&& DrawEntities != 0f
			&& health > 0
			&& header is not null
			&& !NoModel
			&& client.ViewEntityIndex <= client.MaxClients;

		var renderModel = header is null ? null : client.GetRenderModel( modelIndex );

		if ( renderModel != _renderModel )
		{
			_renderModel = renderModel;

			_renderer ??= AddComponent<SkinnedModelRenderer>();
			_renderer.Model = renderModel;
		}

		if ( _renderer is null )
			return;

		_renderer.Enabled = visible;

		if ( !visible )
			return;

		if ( WeaponStartTime == 0.0 || ResentEveryCommand )
			WeaponStartTime = client.ClientTime;

		_state.Sequence = WeaponSequence;
		_state.Frame = 0f;
		_state.FrameRate = 1f;
		_state.AnimTime = (float)WeaponStartTime;
		_state.Origin = Origin;
		_state.Angles = Angles;

		_latched.PrevOrigin = Origin;
		_latched.PrevAngles = Angles;

		var light = Client.Current.StudioDynamicLight( Effects, RenderFx.None, 0, Origin, header.Header.Flags );

		_renderer.Attributes.Set( "AmbientLight", (float)light.AmbientLight );
		_renderer.Attributes.Set( "StudioLight", Client.StudioLight );
		_renderer.Attributes.Set( "ShadeLight", (float)light.ShadeLight );
		_renderer.Attributes.Set( "LightVector", light.LightVector );
		_renderer.Attributes.Set( "LightColor", light.Color );
		_renderer.Attributes.SetCombo( "D_RENDER_MODE", (int)RenderMode.Normal );
		_renderer.Attributes.Set( "NoFog", 1f );
		_renderer.Attributes.Set( "Blend", 1f );
		_renderer.Attributes.Set( "DepthRange", ViewModelDepthRange );

		Client.Current.StudioEntityLight( playerIndex, Origin, Attachments, _renderer.Attributes );

		var angles = Angles;

		angles.x = -angles.x;

		WorldPosition = Origin;
		WorldRotation = Edict.ToRotation( angles );

		var bones = client.BoneSetup;

		bones.SetupBones( header, ref _state, ref _latched, client.ClientTime, Client.StudioInterpolation );

		for ( var i = 0; i < header.BodyParts.Length; i++ )
		{
			ref readonly var bodyPart = ref header.BodyParts[i];

			_renderer.SetBodyGroup( i, Body / bodyPart.Base % bodyPart.NumModels );
		}

		_renderer.UseAnimGraph = false;

		_boneOutput.Apply( client, header, _renderer, WorldTransform, Attachments );

		client.StudioClientEvents( header, _state, _latched, ref Effects, Attachments );

		client.GetEntity( playerIndex )?.CopyAttachments( Attachments );
	}
}
