using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking.Types;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace PBDFluid
{

    public class FluidBoundary : IDisposable
    {
        private const int THREADS = 128;

        public int NumParticles { get; private set; }

        public Bounds Bounds;

        public float ParticleRadius { get; private set; }

        public float ParticleDiameter { get { return ParticleRadius * 2.0f; } }

        public float Density { get; private set; }

        public ComputeBuffer Positions { get; private set; }

        private ComputeBuffer m_argsBuffer;

        private List<int> barrierIndex= new List<int>();
        Vector4[] m_positions;

        public FluidBoundary(ParticlesFromBounds source, float radius, float density, Matrix4x4 RTS)
        {
            NumParticles = source.NumParticles;
            ParticleRadius = radius;
            Density = density;

            CreateParticles(source, RTS);
            CreateBoundryPsi();
        }

        /// <summary>
        /// Draws the mesh spheres when draw particles is enabled.
        /// </summary>
        public void Draw(Camera cam, Mesh mesh, Material material, int layer)
        {
            if (m_argsBuffer == null)
                CreateArgBuffer(mesh.GetIndexCount(0));

            material.SetBuffer("positions", Positions);
            material.SetColor("color", Color.red);
            material.SetFloat("diameter", ParticleDiameter);

            ShadowCastingMode castShadow = ShadowCastingMode.Off;
            bool recieveShadow = false;

            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, Bounds, m_argsBuffer, 0, null, castShadow, recieveShadow, layer, cam);
        }

        public void Dispose()
        {
            if(Positions != null)
            {
                Positions.Release();
                Positions = null;
            }

            CBUtility.Release(ref m_argsBuffer);

        }

        private void CreateParticles(ParticlesFromBounds source, Matrix4x4 RTS)
        {
            m_positions = new Vector4[NumParticles];

            float inf = float.PositiveInfinity;
            Vector3 min = new Vector3(inf, inf, inf);
            Vector3 max = new Vector3(-inf, -inf, -inf);

            for (int i = 0; i < NumParticles; i++)
            {
                Vector4 pos = RTS * source.Positions[i];
                m_positions[i] = pos;

                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
                if (pos.z < min.z) min.z = pos.z;

                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.z > max.z) max.z = pos.z;
            }

            min.x -= ParticleRadius;
            min.y -= ParticleRadius;
            min.z -= ParticleRadius;

            max.x += ParticleRadius;
            max.y += ParticleRadius;
            max.z += ParticleRadius;

            Bounds = new Bounds();
            Bounds.SetMinMax(min, max);

            Positions = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            Positions.SetData(m_positions);

            barrierIndex = source.barrierIndex;
        }

        public void UpdateParticles(Barrier[] barriers) {
            var targetBarriers = barriers.Select(x => x.id).ToArray();
            var positions = new List<Vector4>();

            for (int i = 0; i < m_positions.Length; i++) {
                if (targetBarriers.Contains(barrierIndex[i]) || barrierIndex[i] == -1) {
                    positions.Add(m_positions[i]);
                }
            }

            Positions = new ComputeBuffer(positions.Count, 4 * sizeof(float));
            Positions.SetData(positions);

            NumParticles = positions.Count;

            CreateBoundryPsi();
        }

        private void CreateArgBuffer(uint indexCount)
        {
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = indexCount;
            args[1] = (uint)NumParticles;

            m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_argsBuffer.SetData(args);
        }
        private void CreateBoundryPsi()
        {

            float cellSize = ParticleRadius * 4.0f;
            SmoothingKernel K = new SmoothingKernel(cellSize);

            GridHash grid = new GridHash(Bounds, NumParticles, cellSize);
            grid.Process(Positions);

            ComputeShader shader = Resources.Load("FluidBoundary") as ComputeShader;

            int kernel = shader.FindKernel("ComputePsi");

            shader.SetFloat("Density", Density);
            shader.SetFloat("KernelRadiuse", K.Radius);
            shader.SetFloat("KernelRadius2", K.Radius2);
            shader.SetFloat("Poly6", K.POLY6);
            shader.SetFloat("Poly6Zero", K.Poly6(Vector3.zero));
            shader.SetInt("NumParticles", NumParticles);

            shader.SetFloat("HashScale", grid.InvCellSize);
            shader.SetVector("HashSize", grid.Bounds.size);
            shader.SetVector("HashTranslate", grid.Bounds.min);
            shader.SetBuffer(kernel, "IndexMap", grid.IndexMap);
            shader.SetBuffer(kernel, "Table", grid.Table);

            shader.SetBuffer(kernel, "Boundary", Positions);

            int groups = NumParticles / THREADS;
            if (NumParticles % THREADS != 0) groups++;

            //Fills the boundarys psi array so the fluid can
            //collide against it smoothly. The original computes
            //the phi for each boundary particle based on the
            //density of the boundary but I find the fluid 
            //leaks out so Im just using a const value.

            shader.Dispatch(kernel, groups, 1, 1);

            grid.Dispose();

        }

    }

}