﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ISAAR.MSolve.Analyzers;
using ISAAR.MSolve.Discretization;
using ISAAR.MSolve.Discretization.FreedomDegrees;
using ISAAR.MSolve.FEM.Materials;
using ISAAR.MSolve.IGA.Elements.Boundary;
using ISAAR.MSolve.IGA.Entities;
using ISAAR.MSolve.IGA.Entities.Loads;
using ISAAR.MSolve.IGA.Readers;
using ISAAR.MSolve.LinearAlgebra.Output;
using ISAAR.MSolve.Materials;
using ISAAR.MSolve.MultiscaleAnalysis;
using ISAAR.MSolve.Problems;
using ISAAR.MSolve.Solvers;
using ISAAR.MSolve.Solvers.Direct;
using Troschuetz.Random;
using Xunit;

namespace ISAAR.MSolve.IGA.Tests
{
    public class PaperExamples
    {
        //[Fact]
        public static void ScordelisLoShell()
        {
            var filename = "ScordelisLoShell";
            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "InputFiles", $"{filename}.txt")
                .ToString(CultureInfo.InvariantCulture);
            var numberOfRealizations = 20000;
            var trandom = new TRandom();

            var youngModulusSolutionPairs = new double[numberOfRealizations, 2];

            for (int realization = 0; realization < numberOfRealizations; realization++)
            {
                // Data from https://www.researchgate.net/figure/Mechanical-properties-of-the-nano-hydroxyapatite-polyetheretherketone-nha-PeeK_tbl1_265175039
                var randomInnerE = trandom.Normal(3.4e9, 0.2e9);
                youngModulusSolutionPairs[realization, 0] = randomInnerE;
                var outterMaterial = new ElasticMaterial3DtotalStrain()
                {
                    YoungModulus = 4.3210e8,
                    PoissonRatio = 0.0
                };
                var innerMaterial = new ElasticMaterial3DtotalStrain()
                {
                    YoungModulus = randomInnerE,
                    PoissonRatio = 0.0
                };
                var homogeneousRveBuilder1 = new CompositeMaterialModeluilderTet2(outterMaterial, innerMaterial, 100, 100, 100);
                //var material = new MicrostructureShell2D(homogeneousRveBuilder1,
                //    microModel => (new SuiteSparseSolver.Builder()).BuildSolver(microModel), false, 1);

                var material4 = new Shell2dRVEMaterialHostConst(1, 1, 1, homogeneousRveBuilder1,
                    constModel => (new SuiteSparseSolver.Builder()).BuildSolver(constModel));

                var modelReader = new IsogeometricShellReader(GeometricalFormulation.NonLinear, filepath, material4);
                var model = modelReader.GenerateModelFromFile();

                model.SurfaceLoads.Add(new SurfaceDistributedLoad(-90, StructuralDof.TranslationY));

                // Rigid diaphragm for AB
                for (var i = 0; i < 19; i++)
                {
                    model.ControlPointsDictionary[i * 19].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationX });
                    model.ControlPointsDictionary[i * 19].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationY });
                }

                // Symmetry for CD
                for (var i = 0; i < 19; i++)
                {
                    model.ControlPointsDictionary[i * 19 + 18].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationZ });

                    model.AddPenaltyConstrainedDofPair(new PenaltyDofPair(
                        new NodalDof(model.ControlPointsDictionary[i * 19 + 18], StructuralDof.TranslationX),
                        new NodalDof(model.ControlPointsDictionary[i * 19 + 17], StructuralDof.TranslationX)));
                    model.AddPenaltyConstrainedDofPair(new PenaltyDofPair(
                        new NodalDof(model.ControlPointsDictionary[i * 19 + 18], StructuralDof.TranslationY),
                        new NodalDof(model.ControlPointsDictionary[i * 19 + 17], StructuralDof.TranslationY)));
                }

                // Symmetry for AD
                for (var j = 0; j < 19; j++)
                {
                    model.ControlPointsDictionary[j].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationX });
                    model.AddPenaltyConstrainedDofPair(new PenaltyDofPair(
                        new NodalDof(model.ControlPointsDictionary[j], StructuralDof.TranslationY),
                        new NodalDof(model.ControlPointsDictionary[j + 19], StructuralDof.TranslationY)));
                    model.AddPenaltyConstrainedDofPair(new PenaltyDofPair(
                        new NodalDof(model.ControlPointsDictionary[j], StructuralDof.TranslationZ),
                        new NodalDof(model.ControlPointsDictionary[j + 19], StructuralDof.TranslationZ)));
                }

                // Solvers
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


                var cp = model.ControlPointsDictionary.Values.Last();
                var dofA = model.GlobalDofOrdering.GlobalFreeDofs[cp, StructuralDof.TranslationY];

                var solution = solver.LinearSystems[0].Solution[dofA];

                youngModulusSolutionPairs[realization, 1] = solution;
            }

            var writer = new Array2DWriter();
            writer.WriteToFile(youngModulusSolutionPairs, Path.Combine(Directory.GetCurrentDirectory(), "ScordelisLoMultiscaleResults"));
        }

        //[Fact]
        public void SimpleHoodBenchmark()
        {
            Model model = new Model();
            var filename = "attempt2";
            string filepath = Path.Combine(Directory.GetCurrentDirectory(), "InputFiles",$"{filename}.iga");
            var modelReader = new IgaFileReader(model, filepath);

            modelReader.CreateTSplineShellsModelFromFile(IgaFileReader.TSplineShellType.Thickness, new ShellElasticMaterial2Dtransformationb()
            {
                PoissonRatio = 0.3,
                YoungModulus = 10000
            });

            for (int i = 0; i < 100; i++)
            {
                var id = model.ControlPoints.ToList()[i].ID;
                model.ControlPointsDictionary[id].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationX });
                model.ControlPointsDictionary[id].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationY });
                model.ControlPointsDictionary[id].Constraints.Add(new Constraint() { DOF = StructuralDof.TranslationZ });
            }

            for (int i = model.ControlPoints.Count() - 100; i < model.ControlPoints.Count(); i++)
            {
                var id = model.ControlPoints.ToList()[i].ID;
                model.Loads.Add(new Load()
                {
                    Amount = 100,
                    Node = model.ControlPointsDictionary[id],
                    DOF = StructuralDof.TranslationZ
                });
            }

            var solverBuilder = new SuiteSparseSolver.Builder();
            ISolver solver = solverBuilder.BuildSolver(model);

            // Structural problem provider
            var provider = new ProblemStructural(model, solver);

            // Linear static analysis
            var childAnalyzer = new LinearAnalyzer(model, solver, provider);
            var parentAnalyzer = new StaticAnalyzer(model, solver, provider, childAnalyzer);

            // Run the analysis
            parentAnalyzer.Initialize();
            parentAnalyzer.Solve();

            //var paraview= new ParaviewTsplineShells(model, solver.LinearSystems[0].Solution,filename);
            //paraview.CreateParaviewFile();
        }

    }
}
