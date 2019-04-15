﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ISAAR.MSolve.Analyzers;
using ISAAR.MSolve.Discretization;
using ISAAR.MSolve.Discretization.FreedomDegrees;
using ISAAR.MSolve.Discretization.Mesh;
using ISAAR.MSolve.Discretization.Mesh.Custom;
using ISAAR.MSolve.Discretization.Mesh.GMSH;
using ISAAR.MSolve.FEM.Elements;
using ISAAR.MSolve.FEM.Entities;
using ISAAR.MSolve.Discretization.Mesh;
using ISAAR.MSolve.Logging.VTK;
using ISAAR.MSolve.Materials;
using ISAAR.MSolve.Materials.VonMisesStress;
using ISAAR.MSolve.Problems;
using ISAAR.MSolve.Solvers;
using ISAAR.MSolve.Solvers.Direct;

namespace ISAAR.MSolve.SamplesConsole.FEM
{
    /// <summary>
    /// A 2D cantilever beam modeled with continuum finite elements.
    /// Authors: Serafeim Bakalakos
    /// </summary>
    public class Cantilever2D
    {
        private const double length = 4.0;
        private const double height = 20.0;
        private const double thickness = 0.1;
        private const double youngModulus = 2E6;
        private const double poissonRatio = 0.3;
        private const double maxLoad = 1000.0; // TODO: this should be triangular

        private const string workingDirectory = @"C:\Users\Serafeim\Desktop\Presentation";
        private static readonly string projectDirectory =
            Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + @"\Resources\GMSH";

        public static void Run()
        {
            // Choose one of the mesh files bundled with the project
            //string meshPath = projectDirectory + "\\cantilever_quad4.msh";
            //string meshPath = projectDirectory + "\\cantilever_quad8.msh";
            //string meshPath = projectDirectory + "\\cantilever_quad9.msh";
            //string meshPath = projectDirectory + "\\cantilever_tri3.msh";
            string meshPath = projectDirectory + "\\cantilever_tri6.msh";

            // Or set a path on your machine
            //string meshPath = @"C:\Users\Serafeim\Desktop\Presentation\cantilever.msh";


            (IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) = GenerateMeshFromGmsh(meshPath);
            //(IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) = GenerateUniformMesh();
            //(IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) = GenerateMeshManually();

            Model model = CreateModel(nodes, elements);
            //PrintMeshOnly(model);
            SolveLinearStatic(model);
        }

        private static Model CreateModel(IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements)
        {
            // Initialize
            int numNodes = nodes.Count;
            int numElements = elements.Count;

            // Materials
            ElasticMaterial2D material = new ElasticMaterial2D(StressState2D.PlaneStress)
            {
                YoungModulus = youngModulus,
                PoissonRatio = poissonRatio
            };

            // Subdomains
            Model model = new Model();
            model.SubdomainsDictionary.Add(0, new Subdomain(0));

            // Nodes
            for (int i = 0; i < numNodes; ++i) model.NodesDictionary.Add(i, nodes[i]);

            // Elements
            var factory = new ContinuumElement2DFactory(thickness, material, null);
            for (int i = 0; i < numElements; ++i)
            {
                ContinuumElement2D element = factory.CreateElement(elements[i].CellType, elements[i].Vertices);
                var elementWrapper = new Element() { ID = i, ElementType = element };
                foreach (Node node in element.Nodes) elementWrapper.AddNode(node);
                model.ElementsDictionary.Add(i, elementWrapper);
                model.SubdomainsDictionary[0].Elements.Add(elementWrapper);
            }

            // Constraints
            double tol = 1E-10;
            Node[] constrainedNodes = nodes.Where(node => Math.Abs(node.Y) <= tol).ToArray();
            for (int i = 0; i < constrainedNodes.Length; i++)
            {
                constrainedNodes[i].Constraints.Add(new Constraint { DOF = StructuralDof.TranslationX });
                constrainedNodes[i].Constraints.Add(new Constraint { DOF = StructuralDof.TranslationY });
            }

            // Loads
            Node[] loadedNodes = nodes.Where(
                node => (Math.Abs(node.Y - height) <= tol) && ((Math.Abs(node.X) <= tol))).ToArray();
            if (loadedNodes.Length != 1) throw new Exception("Only 1 node was expected at the top left corner");
            model.Loads.Add(new Load() { Amount = maxLoad, Node = loadedNodes[0], DOF = StructuralDof.TranslationX });

            return model;
        }

        private static (IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) GenerateMeshManually()
        {
            Node[] nodes =
            {
                new Node { ID = 0, X = 0.0,    Y = 0.0 },
                new Node { ID = 1, X = length, Y = 0.0 },
                new Node { ID = 2, X = 0.0,    Y = 0.25 * height },
                new Node { ID = 3, X = length, Y = 0.25 * height },
                new Node { ID = 4, X = 0.0,    Y = 0.50 * height },
                new Node { ID = 5, X = length, Y = 0.50 * height },
                new Node { ID = 6, X = 0.0,    Y = 0.75 * height },
                new Node { ID = 7, X = length, Y = 0.75 * height },
                new Node { ID = 8, X = 0.0,    Y = height },
                new Node { ID = 9, X = length, Y = height }
            };

            CellType[] cellTypes = { CellType.Quad4, CellType.Quad4, CellType.Quad4, CellType.Quad4 };

            CellConnectivity<Node>[] elements =
            {
                new CellConnectivity<Node>(CellType.Quad4, new Node[] { nodes[0], nodes[1], nodes[3], nodes[2]}),
                new CellConnectivity<Node>(CellType.Quad4, new Node[] { nodes[2], nodes[3], nodes[5], nodes[4]}),
                new CellConnectivity<Node>(CellType.Quad4, new Node[] { nodes[4], nodes[5], nodes[7], nodes[6]}),
                new CellConnectivity<Node>(CellType.Quad4, new Node[] { nodes[6], nodes[7], nodes[9], nodes[8]})
            };

            return (nodes, elements);
        }

        private static (IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) GenerateMeshFromGmsh(string path)
        {
            using (var reader = new GmshReader<Node>(path))
            {
                return reader.CreateMesh((id, x, y, z) => new Node() { ID = id, X = x, Y = y, Z = z });
            }
        }

        private static (IReadOnlyList<Node> nodes, IReadOnlyList<CellConnectivity<Node>> elements) GenerateUniformMesh()
        {
            var meshGen = new UniformMeshGenerator2D<Node>(0.0, 0.0, length, height, 4, 20);
            return meshGen.CreateMesh((id, x, y, z) => new Node() { ID = id, X = x, Y = y, Z = z });
        }

        private static void PrintMeshOnly(Model model)
        {
            var mesh = new VtkMesh2D(model);
            using (var writer = new VtkFileWriter(workingDirectory + "\\mesh.vtk"))
            {
                writer.WriteMesh(mesh.Points, mesh.Cells);
            }
        }

        private static void SolveLinearStatic(Model model)
        {
            // Choose linear equation system solver
            var solverBuilder = new SkylineSolver.Builder();
            ISolver solver = solverBuilder.BuildSolver(model);

            // Structural problem provider
            var provider = new ProblemStructural(model, solver);

            // Linear static analysis
            var childAnalyzer = new LinearAnalyzer(model, solver, provider);
            var parentAnalyzer = new StaticAnalyzer(model, solver, provider, childAnalyzer);

            // Run the analysis
            parentAnalyzer.Initialize();
            parentAnalyzer.Solve();

            // Logging displacement, strain, and stress fields.
            string outputDirectory = workingDirectory + "\\Plots";
            childAnalyzer.LogFactories[0] = new VtkLogFactory(model, outputDirectory)
            {
                LogDisplacements = true,
                LogStrains = true,
                LogStresses = true,
                VonMisesStressCalculator = new PlaneStressVonMises()
            };

            // Run the analysis
            parentAnalyzer.BuildMatrices();
            parentAnalyzer.Initialize();
            parentAnalyzer.Solve();
        }
    }
}
