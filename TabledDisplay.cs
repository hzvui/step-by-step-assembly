using System;
using System.Collections;
using System.Collections.Generic;
using Unigine;

[Component(PropertyGuid = "1b2e4ff31b40c23d2cde0f856bae72a6ed6f3dd4")]
public class TabletDisplay : Component
{
    public ObjectGui tabletMesh;
    private Gui tabletGui;
    private WidgetWindow mainContainer;
    private WidgetLabel instructionLabel;

    void Init()
    {
        if (tabletMesh == null)
        {
            Log.Error("TabletDisplay: tabletMesh not assigned!");
            return;
        }

        // tabletMesh = node as ObjectGui;

        tabletGui = tabletMesh.GetGui();

        if (tabletGui == null)
        {
            Log.Error("TabletDisplay: Failed to get GUI from tabletMesh!");
            return;
        }

        CreateTabletUI();
    }

    private void CreateTabletUI()
    {
        
        mainContainer = new WidgetWindow(tabletGui, "Инструкция по сборке");
        mainContainer.Width = tabletMesh.ScreenWidth;   // ������������� ������
        mainContainer.Height = tabletMesh.ScreenHeight; // ������������� ������
        mainContainer.Arrange();
        tabletGui.AddChild(mainContainer, Gui.ALIGN_EXPAND);

        // ����� ����������
        instructionLabel = new WidgetLabel(tabletGui);
        instructionLabel.FontSize = 30;
        instructionLabel.Width = tabletMesh.ScreenWidth-100;
        instructionLabel.Height = tabletMesh.ScreenHeight-100;
        instructionLabel.SetPosition(10, 10); // левый верхний угол
        instructionLabel.FontWrap = 1;
        instructionLabel.Text = "...";
        instructionLabel.TextAlign = Gui.ALIGN_LEFT;
        mainContainer.AddChild(instructionLabel);

        
        //tabletGui.AddChild(mainContainer, Gui.ALIGN_TOP);
    }

    public void SetInstructionText(string text)
    {
        if (instructionLabel != null)
            instructionLabel.Text = text;
    }
}