﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField]
    private float _range = 2f;

    private IDoor _doorImplementation;
    private Transform _target;
    private Vector3 _secondPoint;
    private int _side;

    // Inicializace proměnných
    private void Awake()
    {
        _secondPoint = transform.position + transform.right;
        _doorImplementation = GetComponent<IDoor>();
    }

    // Provádí se jednou za daný interval; kontroluje jestli hráč neprošel dveřmi
    void FixedUpdate()
    {
        if (_side != GetSide(_target.position))
        {
            _side = GetSide(_target.position);
            if (Vector3.Distance(_target.position, transform.position) < _range)
            {
                _doorImplementation.Entered();
            }
        }
    }

    // Při aktivaci získá odkaz na hráčovu pozici a určí, kde se vůči dveřím nachází
    public void OnEnable()
    {
        _target = GameManager.Instance.Player.transform;
        _side = GetSide(_target.position);
        _doorImplementation.Enabled();
    }

    // Vrátí číslo od -1 do 1 podle toho, v kterém poloprostoru se hráč nachází
    public int GetSide(Vector3 position)
    {
        // d = (x−x1)(y2−y1)−(y−y1)(x2−x1); 2x2 matrix determinant
        float temp = (position.x - transform.position.x) * (_secondPoint.z - transform.position.z) - (position.z - transform.position.z) * (_secondPoint.x - transform.position.x);

        if (temp < 0)
        {
            return -1;
        }
        else if (temp == 0)
        {
            return 0;
        }
        else
        {
            return 1;
        }
    }
}
