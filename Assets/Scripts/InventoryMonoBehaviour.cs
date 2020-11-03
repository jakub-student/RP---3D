﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class InventoryMonoBehaviour : MonoBehaviour
{
    public int Coins { get; set; } // TODO loading and saving

    [Header("General")]
    [SerializeField]
    private Canvas _inventoryCanvas;
    [SerializeField]
    private Canvas _inGameCanvas;
    [SerializeField]
    private InventorySlotContainer _playerInventoryContainer;
    [SerializeField]
    private bool _isShop;
    [SerializeField]
    private GameObject _shopTabButton;
    [SerializeField]
    private PlayerController _player;

    [Header("ItemsUI")]
    [SerializeField]
    private GameObject _itemUI;
    [SerializeField]
    private Transform _slotHolder;
    [SerializeField]
    private GameObject _slotPrefab;

    [Header("DescriptionUI")]
    [SerializeField]
    private GameObject _descriptionUI;
    [SerializeField]
    private TextMeshProUGUI _itemNameTMPT;
    [SerializeField]
    private TextMeshProUGUI _descriptionTMPT;
    [SerializeField]
    private Button _equipBuyButton;
    
    [Header("StatsUI")]
    [SerializeField]
    private GameObject _statsUI;
    [SerializeField]
    private TextMeshProUGUI _currentStatsTMPT;
    [SerializeField]
    private TextMeshProUGUI _statsDeltaTMPT;

    private InventorySlotContainer _secondaryShopInventoryContainer;

    private string _savePath;
    private int _currentItem;
    private TextMeshProUGUI _equipBuyButtonTMPT;

    // Current equipment
    private InventorySlot _equippedWeaponSlot;

    public void Awake()
    {
        _equipBuyButtonTMPT = _equipBuyButton.GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Start()
    {
        HideInventoryUI();
        PassStatsToPlayer();
    }

    public void ShowInventory(bool shop)
    {
        _isShop = shop;

        if (_isShop)
        {
            _shopTabButton.SetActive(true);
            DisplayInventory(_secondaryShopInventoryContainer);
        } else
        {
            _shopTabButton.SetActive(false);
            DisplayInventory(_playerInventoryContainer);
        }

        _inventoryCanvas.enabled = true;
        _inGameCanvas.enabled = false;
    }

    public void DisplayInfo(int itemID)
    {
        InventorySlotContainer inventoryContainer;

        if (_isShop)
        {
            inventoryContainer = _secondaryShopInventoryContainer;
        } else
        {
            inventoryContainer = _playerInventoryContainer;
        }

        ItemObject itemObject = inventoryContainer.GetSlotByItemID(itemID).ItemObject;

        _itemNameTMPT.text = itemObject.name;
        _descriptionTMPT.text = itemObject.description;
        _equipBuyButton.onClick.RemoveAllListeners();

        if (_isShop)
        {
            _equipBuyButtonTMPT.SetText("Buy");
            int id = itemObject.itemID;
            _equipBuyButton.onClick.AddListener(delegate { BuyItem(id); });
        } else
        {
            // TODO if equiped -> unequip

            int id = itemObject.itemID;
            if (itemObject is ConsumableObject)
            {
                _equipBuyButtonTMPT.SetText("Consume");
                _equipBuyButton.onClick.AddListener(delegate { ConsumeItem(id); });
            } else
            {
                _equipBuyButtonTMPT.SetText("Equip");
                _equipBuyButton.onClick.AddListener(delegate { EquipItem(id); });

                // TODO change chanseStatsUI
                _currentStatsTMPT.text = _player.GetStats().StatsToStringColumn(false, false);
                // TODO change statsDelta
                CharacterStats selectedObjectStats = EquipmentToStats(itemObject);
                CharacterStats equippedObjectStats = new CharacterStats(); ;
                switch (itemObject.type)
                {
                    case ItemType.Weapon:
                        if(_equippedWeaponSlot != null)
                        {
                            equippedObjectStats = EquipmentToStats(_equippedWeaponSlot.ItemObject);
                        }
                        break;
                }

                selectedObjectStats.SubtractStats(equippedObjectStats);
                _statsDeltaTMPT.text = selectedObjectStats.StatsToStringColumn(true, true);
            }
        }
    }

    public void DisplayInventory(InventorySlotContainer inventory)
    {
        int counter = 0;

        foreach (Transform child in _slotHolder)
        {
            if (counter < inventory.Slots.Count)
            {
                child.gameObject.SetActive(true);

                SetSlotDescription(child.gameObject, inventory.Slots[counter], counter);
            } else
            {
                child.gameObject.SetActive(false);
            }
            counter++;
        }

        for (; counter < inventory.Slots.Count; counter++)
        {
            GameObject newGO = Instantiate(_slotPrefab, _slotHolder);

            SetSlotDescription(newGO, inventory.Slots[counter], counter);
        }

        if (inventory.Slots.Count > 0)
        {
            DisplayInfo(inventory.Slots[0].ItemObject.itemID);
        }
    }

    public void SetSlotDescription(GameObject slot, InventorySlot item, int index)
    {
        slot.GetComponent<Image>().sprite = item.ItemObject.uiSprite;
        slot.GetComponent<Button>().onClick.RemoveAllListeners();
        slot.GetComponent<Image>().color = Color.white; // TODO do smth else

        item.SlotHolderChildPosition = index;
        int id = item.ItemObject.itemID;

        if (!_isShop)
        {
            if (CheckIfEquipped(item))
            {
                slot.GetComponent<Image>().color = Color.green; // TODO do smth else
            }
        }

        slot.GetComponent<Button>().onClick.AddListener(delegate { DisplayInfo(id); });

        if (_isShop)
        {
            slot.GetComponentInChildren<TextMeshProUGUI>().enabled = true;
            slot.GetComponentInChildren<TextMeshProUGUI>().text = item.ItemObject.price.ToString();
        }
        else
        {
            if (item.Amount > 1)
            {
                slot.GetComponentInChildren<TextMeshProUGUI>().enabled = true;
                slot.GetComponentInChildren<TextMeshProUGUI>().text = item.Amount.ToString();
            }
            else
            {
                slot.GetComponentInChildren<TextMeshProUGUI>().enabled = false;
            }
        }
    }

    private bool CheckIfEquipped(InventorySlot slot)
    {
        if(_equippedWeaponSlot != null)
        {
            if (slot.ItemObject.itemID == _equippedWeaponSlot.ItemObject.itemID)
            {
                return true;
            }
        }

        return false;
    }

    // TODO
    public void EquipItem(int itemID)
    {
        InventorySlot inventorySlot = _playerInventoryContainer.GetSlotByItemID(itemID);

        EquipItemInSlot(inventorySlot, true);
        PassStatsToPlayer();
        DisplayInfo(itemID);
    }

    public void EquipItemInSlot(InventorySlot inventorySlot, bool visualEffect)
    {
        if (inventorySlot.ItemObject.GetType() == typeof(WeaponObject)) // ADD CATEGORY
        {
            WeaponObject weapon = (WeaponObject)inventorySlot.ItemObject;
            if (visualEffect)
            {
                if (_equippedWeaponSlot != null)
                {
                    _slotHolder.GetChild(_equippedWeaponSlot.SlotHolderChildPosition).GetComponent<Image>().color = Color.white;  // TODO do smth else
                }
                _slotHolder.GetChild(inventorySlot.SlotHolderChildPosition).GetComponent<Image>().color = Color.green; // TODO do smth else
            }

            _equippedWeaponSlot = inventorySlot;
            _player.SwitchAnimationController(weapon.animationType);
            _player.SetWeapons(weapon.model, weapon.animationType == AnimationType.TWOHANDED);
        }
    }

    public void ConsumeItem(int itemID)
    {
        // TODO
        Debug.Log("CONSUME id " + itemID);
    }

    public void BuyItem(int itemID)
    {
        InventorySlot inventorySlot = _secondaryShopInventoryContainer.GetSlotByItemID(itemID);

        if (_secondaryShopInventoryContainer.RemoveItem(inventorySlot.ItemObject.itemID)) {
            _slotHolder.transform.GetChild(inventorySlot.SlotHolderChildPosition).gameObject.SetActive(false);

            if (_secondaryShopInventoryContainer.Slots.Count > 0)
            {
                DisplayInfo(_secondaryShopInventoryContainer.Slots[0].ItemObject.itemID);
            }
        }

        _playerInventoryContainer.AddItem(inventorySlot.ItemObject, 1);

        Coins -= inventorySlot.ItemObject.price;
    }

    public void PassStatsToPlayer()
    {
        _player.SetStats(GetFullEquipmentStats());
    }

    private CharacterStats GetFullEquipmentStats()
    {
        CharacterStats stats = new CharacterStats();

        if (CheckIfEquipped(_equippedWeaponSlot)) // ADD CATEGORY
        {
            stats.AddStats(EquipmentToStats(_equippedWeaponSlot.ItemObject));
        }

        return stats;
    }

    private CharacterStats EquipmentToStats(ItemObject equipment)
    {
        CharacterStats stats = new CharacterStats();

        if (equipment == null)
        {
            return stats;
        }

        if (equipment.type == ItemType.Weapon) // ADD CATEGORY
        {
            WeaponObject weapon = (WeaponObject)equipment;
            stats.Damage += weapon.damage;
            stats.Health += weapon.healthBonus;
            stats.ArmourPenetration += weapon.armourPenetration;
        }

        return stats;
    }

    #region Item List Manipulation // TODO move some?

    public void AddItem(ItemObject itemObject, int amount)
    {
        _playerInventoryContainer.AddItem(itemObject, amount);
    }

    public void RemoveItem(int itemObjectID)
    {
        _playerInventoryContainer.RemoveItem(itemObjectID);
    }

    public void Save()
    {
        if (_playerInventoryContainer != null)
        {
            _playerInventoryContainer.EquippedItemSlots = new List<InventorySlot>(); // ADD CATEGORY

            if(_equippedWeaponSlot != null)
            {
                _playerInventoryContainer.EquippedItemSlots.Add(_equippedWeaponSlot);
            }

            _playerInventoryContainer.Save();
        }

        if (_secondaryShopInventoryContainer != null)
        {
            _secondaryShopInventoryContainer.Save();
        }
    }

    public void Load(string path)
    {
        _playerInventoryContainer = new InventorySlotContainer(path);

        // TODO else add starting item? can it be even here?
        foreach (InventorySlot slot in _playerInventoryContainer.EquippedItemSlots)
        {
            EquipItemInSlot(slot, false);
        }

        PassStatsToPlayer();
    }

    public void LoadAndOpenShop(InventorySlotContainer container)
    {
        if (_secondaryShopInventoryContainer != null)
        {
            _secondaryShopInventoryContainer.Save();
        }

        _secondaryShopInventoryContainer = container;
        ShowInventory(true);
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    #endregion

    #region UI Methods

    public void SwitchToInventoryTabUI()
    {
        Debug.Log("Player inventory");
        _isShop = false;
        DisplayInventory(_playerInventoryContainer);
    }

    public void SwitchToShopTabUI()
    {
        Debug.Log("Shop inventory");
        _isShop = true;
        DisplayInventory(_secondaryShopInventoryContainer);
    }

    public void HideInventoryUI()
    {
        _inventoryCanvas.enabled = false;
        _inGameCanvas.enabled = true;

        Save();
    }

    #endregion
}