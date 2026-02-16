using System;
using Unigine;
using System.Collections.Generic;

#region Math Variables
#if UNIGINE_DOUBLE
	using Scalar = System.Double;
	using Vec2 = Unigine.dvec2;
	using Vec3 = Unigine.dvec3;
	using Vec4 = Unigine.dvec4;
	using Mat4 = Unigine.dmat4;
#else
using Scalar = System.Single;
using Vec2 = Unigine.vec2;
using Vec3 = Unigine.vec3;
using Vec4 = Unigine.vec4;
using Mat4 = Unigine.mat4;
using WorldBoundBox = Unigine.BoundBox;
using WorldBoundSphere = Unigine.BoundSphere;
using WorldBoundFrustum = Unigine.BoundFrustum;
#endif
#endregion

[Serializable]
public class AssemblyStep
{
    public string Description = "Выполните действие";
    public ObjectMeshStatic SourceObject;
    public vec3 InitialPosition = vec3.ZERO;
    public quat InitialRotation = quat.ZERO;
    public vec3 TargetPosition = vec3.ZERO;
    public float PositionTolerance = 0.10f;
    [ShowInEditor]
    [ParameterSlider(Title = "Допуск по углу (рад)", Min = 0.01f, Max = 1.57f)] // ~0.5° до 90°
    public float RotationTolerance = 0.1745f; // ≈10° по умолчанию

    //добавить rotationTolerance - погрешность для соосности

    [ShowInEditor]
    [ParameterFile]
    public string texturePath;

    [ShowInEditor]
    [Parameter(Title = "Установка с помощью крана", Group = "VR Object Switch")]
    public bool isKran;

    [ShowInEditor]
    [ParameterSlider(Title = "поднять кран", Group = "VR Object Switch")]
    [ParameterCondition(nameof(isKran), 1)]
    public Node buttonUp;

    [ShowInEditor]
    [ParameterSlider(Title = "опустить кран", Group = "VR Object Switch")]
    [ParameterCondition(nameof(isKran), 1)]
    public Node buttonDown;

    [ShowInEditor]
    [ParameterSlider(Title = "изначальное положение кнопки", Group = "VR Object Switch")]
    [ParameterCondition(nameof(isKran), 1)]
    public float ThresholdZ = 0.61f;

    // [ShowInEditor]
    // [ParameterSlider(Title = "изначальное положение кнопки", Group = "VR Object Switch")]
    // [ParameterCondition(nameof(isKran), 1)]
    // public float firstThresholdZ = 0.61f;
}

[Component(PropertyGuid = "29b31f7e73aeef3de90f401e9f0e44f33e490cf6")]
public class AssemblyGuide : Component
{
    [ShowInEditor]
    public AssemblyStep[] Steps = new AssemblyStep[0];

    public InfoWindow InfoWindowComponent;
    public TabletDisplay tabletDisplayComponent;

    [ShowInEditor]
    private ObjectGui gui;

    public int currentStepIndex = -1;
    private Unigine.Object currentGhost = null;
    private vec4[] savedOriginalColors = null;
    private bool isFirstUpdate = true;

    [ShowInEditor]
    [ParameterSlider(Title = "Скорость опускания крана", Min = 0.1f, Max = 5.0f)]
    public float KranDescentSpeed = 1.0f;

    [ShowInEditor]private Material GreenMaterial;

    private Gui realGui;
    private WidgetSprite stepImageSprite;
    private Image currentStepImage = null;
    
    private Test_key_j_and_k heightController;

    void Init()
    {
        if (heightController == null)
            heightController = FindComponentInWorld<Test_key_j_and_k>();

                    if (tabletDisplayComponent == null)
            tabletDisplayComponent = FindComponentInWorld<TabletDisplay>();
            
        if (InfoWindowComponent == null)
            InfoWindowComponent = FindComponentInWorld<InfoWindow>();

        if (Steps == null || Steps.Length == 0)
        {
            Log.Warning("AssemblyGuide: No steps defined!");
            return;
        }

        // Настройка GUI для изображений шагов
        if (gui != null)
        {
            realGui = gui.GetGui();
            stepImageSprite = new WidgetSprite(realGui)
            {
                Width = realGui.Width,
                Height = realGui.Height
            };
            realGui.AddChild(stepImageSprite, Gui.ALIGN_OVERLAP | Gui.ALIGN_BACKGROUND | Gui.ALIGN_CENTER);
        }

        // Инициализация объектов шагов
        foreach (var step in Steps)
        {
            if (step.SourceObject != null)
            {
                step.SourceObject.WorldPosition = step.InitialPosition;
                step.InitialRotation = step.SourceObject.GetWorldRotation();
                step.SourceObject.Enabled = false;
            }
        }
    }

    void Update()
    {
        if (isFirstUpdate)
        {
            isFirstUpdate = false;
            GoToStep(0);
            return;
        }


        if (currentStepIndex >= 0 && currentStepIndex < Steps.Length && currentGhost!=null)
        {
            var step = Steps[currentStepIndex];
            if (step.isKran && step.SourceObject != null && step.SourceObject.Enabled)
            {
                HandleKranMovement(step);
            }
            else if (!step.isKran && step.SourceObject != null )
            {
                if(Input.IsKeyDown(Input.KEY.I))
                {
                    step.SourceObject.WorldPosition = step.InitialPosition;
                    step.SourceObject.SetWorldRotation(step.InitialRotation);
                }
                var movable = step.SourceObject.GetComponent<VRTransformMovableObject>();
                if(movable!=null && VRInteractionManager.IsGrabbed(movable))
                {
                    float distance = (step.SourceObject.WorldPosition-currentGhost.WorldPosition).Length;
                    bool isClose = distance<step.PositionTolerance;

                    vec4 targetColor = isClose
                    ? new vec4(1.0f,0.1f,0.1f,0.9f)
                    : new vec4(1.0f,0.0f,0.0f,0.5f);

                    int enabledAux = isClose? 1 : 0;

                    for(int i=0;i<currentGhost.NumSurfaces;i++)
                    {
                        currentGhost.SetMaterialParameterFloat4("albedo_color",targetColor,i);
                        currentGhost.SetMaterialState("auxiliary", enabledAux, i);
                    }
                }
            }
        }
    }

    private void HandleKranMovement(AssemblyStep step)
    {
        bool isUpActive = false;
        bool isDownActive = false;

        if (step.buttonUp != null)
            isUpActive = step.buttonUp.Position.z <= step.ThresholdZ;

        if (step.buttonDown != null)
            isDownActive = step.buttonDown.Position.z <= step.ThresholdZ;

        vec3 currentPosition = step.SourceObject.WorldPosition;

        if (isUpActive && !isDownActive)
        {
            // Поднимаем кран (увеличиваем Z)
            float deltaZ = KranDescentSpeed * Game.IFps;
            float newZ = currentPosition.z + deltaZ;
            step.SourceObject.WorldPosition = new vec3(currentPosition.x, currentPosition.y, newZ);
        }
        else if (isDownActive && !isUpActive)
        {
            // Опускаем кран (уменьшаем Z)
            float deltaZ = KranDescentSpeed * Game.IFps;
            float newZ = currentPosition.z - deltaZ;

            // Получаем скорректированную целевую позицию
            Vec3 adjustedTarget = GetAdjustedTargetPosition(step);

            if (newZ <= adjustedTarget.z)
            {
                // Устанавливаем объект в СКОРРЕКТИРОВАННУЮ позицию
                step.SourceObject.WorldPosition = new vec3(
                    step.SourceObject.WorldPosition.x,
                    step.SourceObject.WorldPosition.y,
                    adjustedTarget.z
                );

                SnapAndAlignToTarget(step.SourceObject, step);
                FixObjectInPlace(step.SourceObject);
                GoToStep(currentStepIndex + 1);
                return;
            }

            step.SourceObject.WorldPosition = new vec3(currentPosition.x, currentPosition.y, newZ);
        }
        // Если обе кнопки активны или неактивны — ничего не делаем
    }

    private void GoToStep(int index)
    {
        // Восстановить цвет предыдущего объекта (если был обычный шаг)
        if (currentStepIndex >= 0 && currentStepIndex < Steps.Length)
        {
            var prevStep = Steps[currentStepIndex];
            if (prevStep.SourceObject != null)
            {
                RestoreObjectColor(prevStep.SourceObject);
            }
        }

        // УДАЛЯЕМ GHOST ВСЕГДА при смене шага (включая завершение)
        if (currentGhost != null)
        {
            currentGhost.DeleteLater();
            currentGhost = null;
        }

        currentStepIndex = index;

        // Если все шаги завершены
        if (currentStepIndex >= Steps.Length)
        {
            SetInstructionText("✅ Сборка завершена!");
            // НЕ очищаем изображение — оставляем последнее
            return;
        }

        // Обработка нового шага
        var currentStep = Steps[currentStepIndex];
        SetInstructionText(currentStep.Description);

        // Загрузка изображения шага
        if (!string.IsNullOrEmpty(currentStep.texturePath))
        {
            try
            {
                // Освобождаем предыдущее изображение (если оно было от другого шага)
                if (currentStepImage != null)
                {
                    currentStepImage.Dispose();
                    currentStepImage = null;
                }

                currentStepImage = new Image(currentStep.texturePath);
                stepImageSprite?.SetImage(currentStepImage);
            }
            catch (Exception ex)
            {
                Log.Error($"AssemblyGuide: Failed to load image '{currentStep.texturePath}': {ex.Message}");
                stepImageSprite?.SetImage(null);
            }
        }
        else
        {
            if (currentStepImage != null)
            {
                currentStepImage.Dispose();
                currentStepImage = null;
            }
            stepImageSprite?.SetImage(null);
        }

        // Настройка объекта шага
        if (currentStep.SourceObject != null)
        {
            if (currentStep.isKran)
            {
                currentStep.SourceObject.Enabled = true;
                var movable = currentStep.SourceObject.GetComponent<VRTransformMovableObject>();
                if (movable != null)
                {
                    movable.Enabled = false;
                }
            }
            else
            {
                currentStep.SourceObject.Enabled = true;
                MakeObjectGreen(currentStep.SourceObject);
            }

            Vec3 adjustedTarget = GetAdjustedTargetPosition(currentStep);
            currentStep.SourceObject.WorldPosition = currentStep.InitialPosition;
            CreateGhostObject(currentStep.SourceObject, adjustedTarget);
        }
    }
    private void CreateGhostObject(ObjectMeshStatic originalObject, vec3 worldTargetPosition)
    {
        try
        {
            Node clonedNode = originalObject.Clone();
            currentGhost = clonedNode as Unigine.Object;

            if (currentGhost != null)
            {
                Node parent = originalObject.Parent;

                // Устанавливаем того же родителя
                currentGhost.Parent = parent;

                vec3 localTargetPosition;

                if (parent == null)
                {
                    // Объект в корне мира → локальная = мировая
                    localTargetPosition = worldTargetPosition;
                }
                else
                {
                    // Преобразуем мировую позицию в локальную относительно родителя
                    localTargetPosition = WorldToLocalPosition(parent.WorldTransform, worldTargetPosition);
                }

                currentGhost.Enabled = true;
                currentGhost.Position = localTargetPosition; // локальная позиция!
                currentGhost.Name = $"{originalObject.Name}_TargetGhost";

                // Материал: прозрачно-красный
                for (int i = 0; i < currentGhost.NumSurfaces; i++)
                {
                    try
                    {
                        currentGhost.SetMaterialParameterFloat4("albedo_color", new vec4(1.0f, 0.0f, 0.0f, 0.5f), i);
                    }
                    catch { /* ignore */ }
                }

                // Отключаем физику
                Body body = currentGhost.ObjectBody;
                if (body != null) body.Enabled = false;
                for (int i = 0; i < currentGhost.NumSurfaces; i++)
                    currentGhost.SetCollision(false, i);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorLine($"AssemblyGuide: Error creating ghost object: {ex.Message}");
        }
    }

#if UNIGINE_DOUBLE
private vec3 WorldToLocalPosition(dmat4 parentWorldTransform, vec3 worldPos)
{
    dmat4 inv = MathLib.Invert(parentWorldTransform);
    dvec4 local = inv * new dvec4(worldPos, 1.0);
    return (vec3)local;
}
#else
private vec3 WorldToLocalPosition(mat4 parentWorldTransform, vec3 worldPos)
{
    mat4 inv = MathLib.Inverse(parentWorldTransform);
    vec4 local = inv * new vec4(worldPos, 1.0f);
    return local.xyz;
}
#endif

    private void MakeObjectGreen(ObjectMeshStatic obj)
    {
        int num = obj.NumSurfaces;
        savedOriginalColors = new vec4[num];

        for (int i = 0; i < num; i++)
        {
            try
            {
                savedOriginalColors[i] = obj.GetMaterialParameterFloat4("albedo_color", i);
                //obj.SetMaterialParameterFloat4("albedo_color", new vec4(0.0f, 1.0f, 0.0f, 1.0f), i);
                obj.SetMaterial(GreenMaterial, i);
            }
            catch
            {
                savedOriginalColors[i] = new vec4(1.0f, 1.0f, 1.0f, 1.0f);
            }
        }
    }

    private void RestoreObjectColor(ObjectMeshStatic obj)
    {
        if (savedOriginalColors == null || obj == null) return;

        int num = Math.Min(savedOriginalColors.Length, obj.NumSurfaces);
        for (int i = 0; i < num; i++)
        {
            try
            {
                obj.SetMaterialParameterFloat4("albedo_color", savedOriginalColors[i], i);
            }
            catch { }
        }
        savedOriginalColors = null;
    }

    private void SetInstructionText(string text)
    {
        InfoWindowComponent?.SetInstructionText(text);
        tabletDisplayComponent?.SetInstructionText(text);
    }

    public bool IsObjectCurrentTarget(Unigine.Object obj, out AssemblyStep step)
    {
        step = null;
        if (currentStepIndex >= 0 && currentStepIndex < Steps.Length)
        {
            step = Steps[currentStepIndex];
            return step.SourceObject == obj;
        }
        return false;
    }

public bool IsStepCompletedAtPosition(ObjectMeshStatic obj, AssemblyStep step)
{
    if (obj == null || step == null || currentGhost == null || step.isKran) 
        return false;

    // Мировые координаты — всегда безопасны
    float dist = (obj.WorldPosition - currentGhost.WorldPosition).Length;
    bool posOK = dist <= step.PositionTolerance;
    bool rotOK = AreObjectsAligned(obj, currentGhost, step.RotationTolerance);

    return posOK && rotOK;
}

#if UNIGINE_DOUBLE
private bool AreLocalOrientationsAligned(Unigine.Object a, Unigine.Object b, double angleThresholdRadians = 0.1)
#else
private bool AreLocalOrientationsAligned(Unigine.Object a, Unigine.Object b, float angleThresholdRadians = 0.1f)
#endif
{
    if (a == null || b == null) return false;

    // Получаем локальные Z-векторы (forward)
    vec3 forwardA = (a.Transform * new vec4(0, 0, 1, 0)).xyz;
    vec3 forwardB = (b.Transform * new vec4(0, 0, 1, 0)).xyz;

    forwardA = MathLib.Normalize(forwardA);
    forwardB = MathLib.Normalize(forwardB);

    float dot = forwardA.x * forwardB.x + forwardA.y * forwardB.y + forwardA.z * forwardB.z;
    return dot >= Math.Cos(angleThresholdRadians);
}

#if UNIGINE_DOUBLE
    private bool AreObjectsAligned(Unigine.Object a, Unigine.Object b, double angleThresholdRadians = 0.1)
    {
        if (a == null || b == null) return false;
        dvec3 forwardA = (a.WorldTransform * new dvec4(0, 0, 1, 0)).xyz;
        dvec3 forwardB = (b.WorldTransform * new dvec4(0, 0, 1, 0)).xyz;
        forwardA = MathLib.Normalize(forwardA);
        forwardB = MathLib.Normalize(forwardB);
        double dot = dvec3.Dot(forwardA, forwardB);
        return dot >= Math.Cos(angleThresholdRadians);
    }
#else
    private bool AreObjectsAligned(Unigine.Object a, Unigine.Object b, float angleThresholdRadians = 0.1f)
    {
        if (a == null || b == null) return false;
        vec3 forwardA = (a.WorldTransform * new vec4(0, 0, 1, 0)).xyz;
        vec3 forwardB = (b.WorldTransform * new vec4(0, 0, 1, 0)).xyz;
        forwardA = MathLib.Normalize(forwardA);
        forwardB = MathLib.Normalize(forwardB);
        float dot = forwardA.x * forwardB.x + forwardA.y * forwardB.y + forwardA.z * forwardB.z;
        return dot >= Math.Cos(angleThresholdRadians);
    }
#endif

private void SnapAndAlignToTarget(ObjectMeshStatic obj, AssemblyStep step)
{
    if (obj == null || currentGhost == null) return;

    obj.Position = currentGhost.Position;
    obj.Transform = currentGhost.Transform; // локальная трансформация
}

    public void CompleteCurrentStep(ObjectMeshStatic obj)
    {
        if (currentStepIndex < 0 || currentStepIndex >= Steps.Length) return;
        var step = Steps[currentStepIndex];
        if (step.SourceObject != obj || step.isKran) return;

        SnapAndAlignToTarget(obj, step);
        FixObjectInPlace(obj);
        GoToStep(currentStepIndex + 1);
    }

    private void FixObjectInPlace(ObjectMeshStatic obj)
    {
        if (obj == null) return;

        var movable = obj.GetComponent<VRTransformMovableObject>();
        if (movable != null)
        {
            movable.Enabled = false;
        }

        var body = obj.ObjectBody;
        if (body != null)
        {
            body.Enabled = false;
        }

        var bodyRigid = obj.BodyRigid;
        if (bodyRigid != null)
        {
            bodyRigid.LinearVelocity = vec3.ZERO;
            bodyRigid.AngularVelocity = vec3.ZERO;
        }

        Log.MessageLine($"Object '{obj.Name}' fixed in place.");
    }

    private Vec3 GetAdjustedTargetPosition(AssemblyStep step)
    {
        Vec3 adjusted = step.TargetPosition;
        if (heightController != null)
        {
            adjusted.z += (Scalar)heightController.DeltaZ;
            Log.Message($"[DEBUG] Adjusting target Z: {step.TargetPosition.z} + {heightController.DeltaZ} = {adjusted.z}");
        }
        else
        {
            Log.Warning("[DEBUG] heightController is null!");
        }
        return adjusted;
    }


    void Shutdown()
    {
        if (currentGhost != null)
        {
            currentGhost.DeleteLater();
            currentGhost = null;
        }

        // Освобождаем изображение ТОЛЬКО если оно существует
        if (currentStepImage != null)
        {
            try
            {
                currentStepImage.Dispose();
            }
            catch { /* игнорируем ошибки при выгрузке */ }
            currentStepImage = null;
        }

        // Уничтожаем спрайт, если GUI ещё жив
        if (stepImageSprite != null && realGui != null && !realGui.IsDeleted)
        {
            stepImageSprite.DeleteLater();
        }
        stepImageSprite = null;
    }
}