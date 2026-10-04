using Sandbox.Rendering;

[Title( "Gamma Space" )]
[Category( "Post Processing" )]
[Icon( "gradient" )]
public sealed class GammaSpace : BasePostProcess<GammaSpace>
{
	private static readonly Material Shader = Material.FromShader( "shaders/postprocess/gamma_space.shader" );
	private static readonly Material MaskShader = Material.FromShader( "shaders/postprocess/gamma_space_mask.shader" );

	protected override void OnEnabled()
	{
		Scene.RenderAttributes.SetCombo( "D_GAMMA_SPACE", true );
	}

	protected override void OnDisabled()
	{
		Scene.RenderAttributes.SetCombo( "D_GAMMA_SPACE", false );
	}

	public override void Render()
	{
		var commands = new CommandList( "GammaSpace" );

		commands.Attributes.GrabFrameTexture( "ColorBuffer" );
		commands.Blit( MaskShader, Attributes );
		commands.Attributes.GrabFrameTexture( "GammaBuffer" );
		commands.Blit( Shader, Attributes );

		InsertCommandList( commands, Stage.AfterViewmodel, 0, "GammaSpace" );
	}
}
