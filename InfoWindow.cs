using System.Collections;
using System.Collections.Generic;
using Unigine;

[Component(PropertyGuid = "ff417f460334bb8669f9667b3cb46a062f054e8b")]
public class InfoWindow : Component
{
	public NodeReference windowNodeReference;

	private WidgetWindow window;
	private WidgetLabel contentLabel;

	private void Init()
	{
		InitializeWindow();
	}

	private void InitializeWindow()
	{
		Gui gui = Gui.GetCurrent();

		// Создае окна
		window = new WidgetWindow(gui, "Инструкция");
		window.Width = 150;
		window.Height = 300;

		// Создаем layout
		WidgetVBox vbox = new WidgetVBox(gui);
		vbox.Background = 1;
		window.AddChild(vbox);

		// Заголовок
		WidgetLabel title = new WidgetLabel(gui, "Порядок сборки:");
		title.FontSize = 20;
		title.FontColor = new vec4(1, 1, 1, 1);
		vbox.AddChild(title);

		// Разделитель
		WidgetSpacer spacer = new WidgetSpacer(gui);
		spacer.Height = 15;
		vbox.AddChild(spacer);

		// Контент
		contentLabel = new WidgetLabel(gui);
		contentLabel.FontSize = 14;
		contentLabel.FontColor = new vec4(0.9f, 0.9f, 0.9f, 1.0f);
		contentLabel.Text = GetFormattedContent();
		vbox.AddChild(contentLabel);
		
		gui.AddChild(window, Gui.ALIGN_OVERLAP);
	}

// Добавь ЭТОТ метод в класс InfoWindow
public void SetInstructionText(string text)
{
    if (contentLabel != null)
        contentLabel.Text = text;
}
	private string GetFormattedContent()
	{
		return @"-------------- ------------
		------------------
		----------";
	}

	// Метод для показа/скрытия окна
	public void ToggleWindow()
	{
		if (window != null)
			window.Hidden = !window.Hidden;
	}

	public void ChangeWorld()
	{
		if(window!=null)
			window.DeleteForce();
	}
}