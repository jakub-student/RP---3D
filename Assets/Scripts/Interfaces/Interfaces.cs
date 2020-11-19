﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// TODO split into different files
public interface IDamageable
{
    void TakeDamage(float damage, float armourPenetration);
}

public interface IWinCondition
{
    Action OnCompleted { get; set; } // TODO add coin reward Action<int> || get artefact to sell for coins || reward from shopkeeper?
    List<Vector3> ConfirmSpawnLocations(List<Vector3> array);
    List<GenerationRule> SpecialGenerationRules();
}

public interface IDoor
{
    void Entered();
}

public interface ICellGenerator
{
    CellData GenerateCells(MazeSettingsSO mazeSettings, Vector3 position);
}

public interface ISubcellGenerator
{
    SubcellData GenerateSubcells(MazeSettingsSO mazeSettings, CellData cellData, Vector3 position, int additionalArraySize);
}

public interface ITileGenerator
{
    void GenerateTiles(SubcellData subcellData);
}

public interface IPathfindingNode<T> : IComparable<T>
{
    Status Status { get; set; }
    float GCost { get; set; }
    float HCost { get; set; }
    float FCost { get; }
    int ID { get; set; }
    int CameFromID { get; set; }
    int IndexInHeap { get; set; }
    T[] Neighbours { get; set; }
    Vector3 Position { get; set; }
}