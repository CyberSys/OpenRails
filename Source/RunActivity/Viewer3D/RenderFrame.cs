﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace ORTS
{
    public abstract class RenderPrimitive
    {
        public float ZBias = 0f;
        public int Sequence = 0;

        /// <summary>
        /// This is when the object actually renders itself onto the screen.
        /// Do not reference any volatile data.
        /// Executes in the RenderProcess thread
        /// </summary>
        /// <param name="graphicsDevice"></param>
        public abstract void Draw(GraphicsDevice graphicsDevice);
    }

    public struct RenderItem
    {
        public Material Material;
        public RenderPrimitive RenderPrimitive;
        public Matrix XNAMatrix;

        public RenderItem(Material material, RenderPrimitive renderPrimitive, Matrix xnaMatrix)
        {
            Material = material;
            RenderPrimitive = renderPrimitive;
            XNAMatrix = xnaMatrix;
        }
    }

    public class RenderFrame
    {
        List<RenderItem> RenderItems;
        int RenderMaxSequence = 0;
        Matrix XNAViewMatrix;
        Matrix XNAProjectionMatrix;
        RenderProcess RenderProcess;

        public RenderFrame(RenderProcess owner)
        {
            RenderProcess = owner;
            RenderItems = new List<RenderItem>();
        }

        public void Clear() 
        {
            RenderItems.Clear();
        }

        public void SetCamera(ref Matrix xnaViewMatrix, ref Matrix xnaProjectionMatrix)
        {
            XNAViewMatrix = xnaViewMatrix;
            XNAProjectionMatrix = xnaProjectionMatrix;
        }
        
        /// <summary>
        /// Executed in the UpdateProcess thread
        /// </summary>
        public void AddPrimitive(Material material, RenderPrimitive primitive, ref Matrix xnaMatrix) 
        {
            RenderItems.Add(new RenderItem(material, primitive, xnaMatrix));

            if (RenderMaxSequence < primitive.Sequence)
                RenderMaxSequence = primitive.Sequence;

            // TODO, enhance this:
            //   - handle overflow by enlarging array etc
            //   - to accomodate separate list of shadow casters, 
        }

        /// <summary>
        /// Executed in the UpdateProcess thread
        /// </summary>
        public void Sort()
        {
            // TODO, enhance this:
            //   - to sort translucent primitives
            //   - and to minimize render state changes ( sorting was taking too long! for this )
        }

        /// <summary>
        /// Draw 
        /// Executed in the RenderProcess thread 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        public void Draw(GraphicsDevice graphicsDevice)
        {
            Materials.UpdateShaders(RenderProcess, graphicsDevice);
            if (RenderProcess.Viewer.DynamicShadows)
                DrawShadows(graphicsDevice);
            DrawSimple(graphicsDevice);
        }

        RenderTarget2D ShadowMapRenderTarget;
        DepthStencilBuffer ShadowMapStencilBuffer;
        Texture2D ShadowMap;
        Matrix ShadowMapLightView;
        Matrix ShadowMapLightProj;
        public void DrawShadows(GraphicsDevice graphicsDevice)
        {
            if (ShadowMapRenderTarget == null)
            {
                ShadowMapRenderTarget = new RenderTarget2D(graphicsDevice, 4096, 4096, 1, SurfaceFormat.Single);
                ShadowMapStencilBuffer = new DepthStencilBuffer(graphicsDevice, 4096, 4096, DepthFormat.Depth16);
            }

            var cameraLocation = RenderProcess.Viewer.Camera.Location * new Vector3(1, 1, -1);
            ShadowMapLightView = Matrix.CreateLookAt(cameraLocation + 1000 * RenderProcess.Viewer.SkyDrawer.solarDirection, cameraLocation, Vector3.Up);
            ShadowMapLightProj = Matrix.CreateOrthographic(512, 512, 0.01f, 2500.0f);

            var oldStencilDepthBuffer = graphicsDevice.DepthStencilBuffer;
            graphicsDevice.SetRenderTarget(0, ShadowMapRenderTarget);
            graphicsDevice.DepthStencilBuffer = ShadowMapStencilBuffer;
            graphicsDevice.Clear(Color.Black);
            Materials.ShadowMapMaterial.SetState(graphicsDevice, ShadowMapLightView, ShadowMapLightProj);

            foreach (var renderItem in RenderItems)
            {
                var ri = renderItem;
                if ((renderItem.Material is SceneryMaterial) || (renderItem.Material is ForestMaterial))
                    Materials.ShadowMapMaterial.Render(graphicsDevice, null, renderItem.RenderPrimitive, ref ri.XNAMatrix, ref ShadowMapLightView, ref ShadowMapLightProj);
            }

            graphicsDevice.VertexDeclaration = TerrainPatch.PatchVertexDeclaration;
            graphicsDevice.Indices = TerrainPatch.PatchIndexBuffer;
            foreach (var renderItem in RenderItems)
            {
                var ri = renderItem;
                if (renderItem.Material is TerrainMaterial)
                    Materials.ShadowMapMaterial.Render(graphicsDevice, null, renderItem.RenderPrimitive, ref ri.XNAMatrix, ref ShadowMapLightView, ref ShadowMapLightProj);
            }

            Materials.ShadowMapMaterial.ResetState(graphicsDevice, null);
            graphicsDevice.DepthStencilBuffer = oldStencilDepthBuffer;
            graphicsDevice.SetRenderTarget(0, null);
            ShadowMap = ShadowMapRenderTarget.GetTexture();
        }

        /// <summary>
        /// Executed in the RenderProcess thread - simple draw
        /// </summary>
        /// <param name="graphicsDevice"></param>
        public void DrawSimple(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.Clear(Materials.FogColor);

            for (int iSequence = 0; iSequence <= RenderMaxSequence; ++iSequence)
                DrawSequence(graphicsDevice, iSequence);

        }

        public void DrawSequence(GraphicsDevice graphicsDevice, int sequence)
        {
            if (RenderProcess.Viewer.DynamicShadows)
            {
                Materials.SceneryShader.ShadowMap_Tex = ShadowMap;
                Materials.SceneryShader.LightView = ShadowMapLightView;
                Materials.SceneryShader.LightProj = ShadowMapLightProj;
                Materials.SceneryShader.ShadowMapProj = new Matrix(0.5f, 0, 0, 0, 0, -0.5f, 0, 0, 0, 0, 1, 0, 0.5f + 0.5f / ShadowMapStencilBuffer.Width, 0.5f + 0.5f / ShadowMapStencilBuffer.Height, 0, 1);
            }

            // Render each material on the specified primitive
            // To minimize renderstate changes, the material is
            // told what material was used previously so it can
            // make a decision on what renderstates need to be
            // changed.
            Material prevMaterial = null;
            foreach (var renderItem in RenderItems)
            {
                Material currentMaterial = renderItem.Material;
                if (renderItem.RenderPrimitive.Sequence == sequence)
                {
                    if (prevMaterial != null)
                        prevMaterial.ResetState(graphicsDevice, currentMaterial);
                    var ri = renderItem;
                    currentMaterial.Render(graphicsDevice, prevMaterial, renderItem.RenderPrimitive, ref ri.XNAMatrix, ref XNAViewMatrix, ref XNAProjectionMatrix);
                    prevMaterial = currentMaterial;
                }
            }
            if (prevMaterial != null)
                prevMaterial.ResetState(graphicsDevice, null);
        }
    }
}
