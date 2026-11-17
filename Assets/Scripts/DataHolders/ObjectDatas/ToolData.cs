using System.Collections.Generic;
using UnityEngine;

// Categoria da ferramenta: como ela interage com o mundo.
public enum ToolKind { Hand, Pickaxe, Axe, Shovel }

[CreateAssetMenu(fileName = "ToolData", menuName = "Inventory/Tool Data")]
public class ToolData : ScriptableObject {
    public int id;                             // id único da ferramenta (é o que fica no slot Tool)
    public string displayName;

    [Header("Visual")]
    public Sprite icon;                        // ícone exibido na UI (craft, tooltip, etc.)

    [Header("Visual no robô (por direção)")]
    [Tooltip("Exibido no robô andando pra baixo / parado (frente). Vazio = usa o icon.")]
    public Sprite spriteFrente;
    [Tooltip("Exibido no robô andando pra cima (costas). Vazio = usa o icon.")]
    public Sprite spriteTras;
    [Tooltip("Exibido no robô andando pra direita; a esquerda usa o mesmo espelhado (flipX). Vazio = usa o icon.")]
    public Sprite spriteLado;

    [Header("Compatibilidade")]
    public ToolKind kind;
    public List<ResourceType> acceptedResourceTypes; // tipos de recurso que ela extrai (ResourceData.resourceType)

    [Header("Custo")]
    public int yieldPerUse = 5;   // material extraído por uso
    public int ticksPerUse = 1;   // ticks gastos por uso
    public int maxUses = 20;      // durabilidade (usos)
}
