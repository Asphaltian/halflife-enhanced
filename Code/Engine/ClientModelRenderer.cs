using GoldSrc.Studio;

namespace GoldSrc;

public sealed class ClientModelRenderer : Component
{
	private readonly BoneOutput _boneOutput = new();
	private readonly Vector3[] _attachments = new Vector3[4];

	private ModelRenderer _renderer;
	private readonly SpriteNextView _spriteNextView = new();
	private Model _model;
	private EntityState _state;
	private LatchedVars _latched = new() { PrevController = new byte[4] };

	protected override void OnPreRender()
	{
		_boneOutput.UpdateBounds( _renderer as SkinnedModelRenderer );
	}

	public void Hide()
	{
		if ( _renderer is not null )
			_renderer.Enabled = false;

		_spriteNextView.Hide();
	}

	protected override void OnDestroy() => _spriteNextView.Destroy();

	public void Draw( Client client, TempEnt temp )
	{
		var model = temp.Model;

		if ( model != _model )
		{
			if ( _renderer is not null && _renderer is SkinnedModelRenderer != model is StudioModel )
			{
				_renderer.Destroy();
				_renderer = null;
			}

			_model = model;

			_renderer ??= model is StudioModel ? AddComponent<SkinnedModelRenderer>() : AddComponent<ModelRenderer>();
			_renderer.Model = model is SpriteModel ? client.SpriteQuad : model.RenderModel;
			_renderer.MaterialOverride = null;

			WorldScale = Vector3.One;

			GameObject.Tags.Set( Client.FirstViewTag, model is SpriteModel );
		}

		var blend = temp.RenderMode == RenderMode.Normal ? 255 : (int)temp.RenderAmt;

		_renderer.Enabled = blend > 0;

		if ( !_renderer.Enabled || model is not SpriteModel )
			_spriteNextView.Hide();

		if ( !_renderer.Enabled )
			return;

		_renderer.Attributes.SetCombo( "D_RENDER_MODE", (int)temp.RenderMode );

		var translucent = temp.RenderMode is RenderMode.TransColor or RenderMode.TransTexture or RenderMode.Glow or RenderMode.TransAdd;
		var drawn = true;

		if ( model is SpriteModel sprite )
		{
			var state = new SpriteState
			{
				Origin = temp.Origin,
				Angles = temp.Angles,
				Scale = temp.Scale,
				Frame = temp.Frame,
				RenderMode = temp.RenderMode,
				RenderFx = temp.RenderFx,
				RenderAmt = temp.RenderAmt,
				RenderColor = temp.RenderColor
			};

			_spriteNextView.Draw( client, sprite, state, blend * 0.003921569f );

			_renderer.Enabled = SpriteDraw.DrawModel( Scene.Camera, sprite, _renderer, state, blend * 0.003921569f );
			translucent = true;
		}
		else if ( model is StudioModel studio )
		{
			drawn = DrawStudio( client, studio.File, temp, blend );
		}

		if ( _renderer.SceneObject is not { } sceneObject )
			return;

		if ( model is StudioModel )
			sceneObject.RenderingEnabled = drawn;

		sceneObject.Flags.IsTranslucent = translucent;
		sceneObject.Flags.IsOpaque = !translucent;
		sceneObject.Flags.WantsPrePass = !translucent;
	}

	private bool DrawStudio( Client client, File header, TempEnt temp, int blend )
	{
		var renderer = (SkinnedModelRenderer)_renderer;
		var angles = temp.Angles;

		_state.Sequence = 0;
		_state.Frame = temp.Frame;
		_state.FrameRate = temp.FrameRate;
		_state.AnimTime = 0f;
		_state.Origin = temp.Origin;
		_state.Angles = angles;

		_latched.PrevOrigin = temp.Origin;
		_latched.PrevAngles = angles;

		WorldPosition = temp.Origin;
		WorldRotation = Edict.ToRotation( new Vector3( -angles.x, angles.y, angles.z ) );

		if ( !client.StudioCheckBBox( header, ref _state, temp.Origin, WorldTransform ) )
			return false;

		var light = Client.Current.StudioDynamicLight( (Effects)temp.Effects, temp.RenderFx, 0, temp.Origin, header.Header.Flags );

		renderer.Attributes.Set( "AmbientLight", (float)light.AmbientLight );
		renderer.Attributes.Set( "ShadeLight", (float)light.ShadeLight );
		renderer.Attributes.Set( "LightVector", light.LightVector );
		renderer.Attributes.Set( "LightColor", light.Color );

		Client.Current.StudioEntityLight( 0, temp.Origin, _attachments, renderer.Attributes );
		renderer.Attributes.Set( "Blend", blend / 255f );
		renderer.Attributes.Set( "RenderColor", new Vector3( (byte)(int)temp.RenderColor.x, (byte)(int)temp.RenderColor.y, (byte)(int)temp.RenderColor.z ) );

		client.BoneSetup.SetupBones( header, ref _state, ref _latched, client.ClientTime, Client.StudioInterpolation );

		for ( var i = 0; i < header.BodyParts.Length; i++ )
		{
			ref readonly var bodyPart = ref header.BodyParts[i];

			renderer.SetBodyGroup( i, temp.Body / bodyPart.Base % bodyPart.NumModels );
		}

		if ( renderer.HasMaterialGroups )
			renderer.MaterialGroup = $"Skin {temp.Skin}";

		renderer.UseAnimGraph = false;

		_boneOutput.Apply( client, header, renderer, WorldTransform, _attachments );

		return true;
	}
}
