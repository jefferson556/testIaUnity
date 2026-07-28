using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CatInputReader : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }

    public event Action InteractPressed;

    private void Update()
    {
        MoveInput = Vector2.zero;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        ReadMovement(keyboard);
        ReadInteraction(keyboard);
    }

    private void ReadMovement(Keyboard keyboard)
    {
        if (
            keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed
        )
        {
            MoveInput += Vector2.left;
        }

        if (
            keyboard.dKey.isPressed ||
            keyboard.rightArrowKey.isPressed
        )
        {
            MoveInput += Vector2.right;
        }

        if (
            keyboard.wKey.isPressed ||
            keyboard.upArrowKey.isPressed
        )
        {
            MoveInput += Vector2.up;
        }

        if (
            keyboard.sKey.isPressed ||
            keyboard.downArrowKey.isPressed
        )
        {
            MoveInput += Vector2.down;
        }

        MoveInput = MoveInput.normalized;
    }

    private void ReadInteraction(Keyboard keyboard)
    {
        if (keyboard.eKey.wasPressedThisFrame)
        {
            InteractPressed?.Invoke();
        }
    }
}