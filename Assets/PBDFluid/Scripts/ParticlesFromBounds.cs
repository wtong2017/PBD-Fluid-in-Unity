using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace PBDFluid
{

    public class ParticlesFromBounds : ParticleSource
    {

        public Bounds Bounds { get; private set; }

        public List<Bounds> Exclusion { get; private set; }

        public GameObject m_gameObject;
        public LayerMask m_layerMask;

        public List<int> barrierIndex= new List<int>();

        public ParticlesFromBounds(float spacing, Bounds bounds) : base(spacing) {
            Bounds = bounds;
            Exclusion = new List<Bounds>();
            CreateParticles();
        }

        public ParticlesFromBounds(float spacing, Bounds bounds, Bounds exclusion) : base(spacing) {
            Bounds = bounds;
            Exclusion = new List<Bounds>();
            Exclusion.Add(exclusion);
            CreateParticles();
        }

        public ParticlesFromBounds(float spacing, Bounds bounds, Bounds exclusion, GameObject gameObject) : base(spacing) {
            Bounds = bounds;
            m_gameObject = gameObject;
            Exclusion = new List<Bounds>();
            Exclusion.Add(exclusion);
            CreateParticlesWithGameobject();
        }

        public ParticlesFromBounds(float spacing, Bounds bounds, Bounds exclusion, LayerMask layerMask) : base(spacing) {
            Bounds = bounds;
            m_layerMask = layerMask;
            Exclusion = new List<Bounds>();
            Exclusion.Add(exclusion);
            CreateParticlesWithLayerMask(new Bounds[] { });
        }

        public ParticlesFromBounds(float spacing, Bounds bounds, Bounds exclusion, Bounds[] barriers, LayerMask layerMask) : base(spacing) {
            Bounds = bounds;
            m_layerMask = layerMask;
            Exclusion = new List<Bounds>();
            Exclusion.Add(exclusion);
            CreateParticlesWithLayerMask(barriers);
        }

        void CreateParticlesWithLayerMask(Bounds[] barriers) {
            int numX = (int)((Bounds.size.x + HalfSpacing) / Spacing);
            int numY = (int)((Bounds.size.y + HalfSpacing) / Spacing);
            int numZ = (int)((Bounds.size.z + HalfSpacing) / Spacing);

            Positions = new List<Vector3>();

            ComputeAllRaycast(out NativeArray<RaycastHit> results);

            for (int z = 0; z < numZ; z++) {
                for (int y = 0; y < numY; y++) {
                    for (int x = 0; x < numX; x++) {
                        Vector3 pos = new Vector3();
                        pos.x = Spacing * x + Bounds.min.x + HalfSpacing;
                        pos.y = Spacing * y + Bounds.min.y + HalfSpacing;
                        pos.z = Spacing * z + Bounds.min.z + HalfSpacing;

                        bool exclude = false;
                        int bIndex = -1;
                        var hit = results[x + numX * y + z * numY * numX];
                        var belowLayer = hit.collider != null;
                        var justBelowLayer = false;
                        if (belowLayer) {
                            justBelowLayer = hit.distance < Spacing;
                        }
                        for (int i = 0; i < Exclusion.Count; i++) {
                            var excluded = Exclusion[i].Contains(pos);
                            if ((excluded && !justBelowLayer) || (!excluded && !justBelowLayer && belowLayer)) {
                                exclude = true;
                                break;
                            }
                        }

                        // add back points that are included in the barriers
                        if (exclude) {
                            for (int i =0; i < barriers.Length; i++) {
                                if (barriers[i].Contains(pos) && !Positions.Contains(pos)) {
                                    exclude = false;
                                    bIndex = i;
                                    break;
                                }
                            }
                        }

                        if (!exclude) {
                            Positions.Add(pos);
                            barrierIndex.Add(bIndex);
                        }
                    }
                }
            }

            results.Dispose();
        }

        void CreateParticlesWithGameobject() {
            int numX = (int)((Bounds.size.x + HalfSpacing) / Spacing);
            int numY = (int)((Bounds.size.y + HalfSpacing) / Spacing);
            int numZ = (int)((Bounds.size.z + HalfSpacing) / Spacing);

            Positions = new List<Vector3>();

            for (int z = 0; z < numZ; z++) {
                for (int y = 0; y < numY; y++) {
                    for (int x = 0; x < numX; x++) {
                        Vector3 pos = new Vector3();
                        pos.x = Spacing * x + Bounds.min.x + HalfSpacing;
                        pos.y = Spacing * y + Bounds.min.y + HalfSpacing;
                        pos.z = Spacing * z + Bounds.min.z + HalfSpacing;

                        bool exclude = false;
                        for (int i = 0; i < Exclusion.Count; i++) {
                            if ((Exclusion[i].Contains(pos) && !CheckIfJustBelow(pos)) || (!Exclusion[i].Contains(pos) && !CheckIfJustBelow(pos) && CheckIfBelow(pos))) {
                                exclude = true;
                                break;
                            }
                        }

                        if (!exclude)
                            Positions.Add(pos);
                    }
                }
            }
        }

        void ComputeAllRaycast(out NativeArray<RaycastHit> results) {
            int numX = (int)((Bounds.size.x + HalfSpacing) / Spacing);
            int numY = (int)((Bounds.size.y + HalfSpacing) / Spacing);
            int numZ = (int)((Bounds.size.z + HalfSpacing) / Spacing);
            // Perform a single raycast using RaycastCommand and wait for it to complete
            // Setup the command and result buffers
            int size = numX * numY * numZ;
            results = new NativeArray<RaycastHit>(size, Allocator.TempJob);

            var commands = new NativeArray<RaycastCommand>(size, Allocator.TempJob);
            Vector3 direction = Vector3.up;

            for (int z = 0; z < numZ; z++) {
                for (int y = 0; y < numY; y++) {
                    for (int x = 0; x < numX; x++) {
                        Vector3 pos = new Vector3();
                        pos.x = Spacing * x + Bounds.min.x + HalfSpacing;
                        pos.y = Spacing * y + Bounds.min.y + HalfSpacing;
                        pos.z = Spacing * z + Bounds.min.z + HalfSpacing;

                        commands[x + numX * y + z * numY * numX] = new RaycastCommand(pos, direction, Mathf.Infinity, m_layerMask);
                    }
                }
            }
            // Schedule the batch of raycasts
            JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 1, default(JobHandle));

            // Wait for the batch processing job to complete
            handle.Complete();

            // Dispose the buffers
            commands.Dispose();
        }

        bool CheckIfJustBelowLayer(Vector3 point) {
            Vector3 direction = new Vector3(0, 1, 0);
            if (Physics.Raycast(point, direction, out RaycastHit hit, Mathf.Infinity, m_layerMask)) {
                if (hit.distance < Spacing)
                    return true;
                else return false;
            }

            else return false;
        }

        bool CheckIfBelowLayer(Vector3 point) {
            Vector3 direction = new Vector3(0, 1, 0);
            if (Physics.Raycast(point, direction, Mathf.Infinity, m_layerMask)) {
                return true;
            }

            else return false;
        }

        bool CheckIfJustBelow(Vector3 point) {
            Vector3 direction = new Vector3(0, 1, 0);
            RaycastHit hit;
            if (Physics.Raycast(point, direction, out hit, Mathf.Infinity)) {
                //Debug.Log(hit.collider.gameObject.name);
                if (hit.collider.gameObject == m_gameObject && hit.distance < Spacing)
                    return true;
                else return false;
            }

            else return false;
        }

        bool CheckIfBelow(Vector3 point) {
            Vector3 direction = new Vector3(0, 1, 0);
            RaycastHit hit;
            if (Physics.Raycast(point, direction, out hit, Mathf.Infinity)) {
                //Debug.Log(hit.collider.gameObject.name);
                if (hit.collider.gameObject == m_gameObject)
                    return true;
                else return false;
            }

            else return false;
        }

        private void CreateParticles() {

            int numX = (int)((Bounds.size.x + HalfSpacing) / Spacing);
            int numY = (int)((Bounds.size.y + HalfSpacing) / Spacing);
            int numZ = (int)((Bounds.size.z + HalfSpacing) / Spacing);

            Positions = new List<Vector3>();

            for (int z = 0; z < numZ; z++) {
                for (int y = 0; y < numY; y++) {
                    for (int x = 0; x < numX; x++) {
                        Vector3 pos = new Vector3();
                        pos.x = Spacing * x + Bounds.min.x + HalfSpacing;
                        pos.y = Spacing * y + Bounds.min.y + HalfSpacing;
                        pos.z = Spacing * z + Bounds.min.z + HalfSpacing;

                        bool exclude = false;
                        for (int i = 0; i < Exclusion.Count; i++) {
                            if (Exclusion[i].Contains(pos)) {
                                exclude = true;
                                break;
                            }
                        }

                        if (!exclude)
                            Positions.Add(pos);
                    }
                }
            }

        }

    }

}