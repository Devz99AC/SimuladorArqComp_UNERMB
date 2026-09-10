using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controlador UI Toolkit del Módulo Teórico.
///
/// Sustituye a la lista plana de 20 temas por la estructura real del
/// contenido analítico de la unidad curricular: <b>unidad → tema</b>, servida
/// por un <see cref="TheorySyllabusSO"/> (data-driven: cambiar el temario es
/// editar ese asset, no la escena ni este script).
///
/// <para>Tres estados dentro del mismo documento, alternados por display:</para>
/// <list type="bullet">
///   <item><b>Portada</b> — datos de la unidad curricular y tarjetas de unidad.</item>
///   <item><b>Lector</b> — un tema, con el contenido convertido a elementos con
///         jerarquía por <see cref="TheoryMarkup"/>.</item>
///   <item><b>Bibliografía</b> — las referencias del programa.</item>
/// </list>
///
/// El índice lateral está siempre visible: es lo que permite al estudiante
/// saber dónde está y saltar a cualquier tema sin volver atrás.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TheoryView : MonoBehaviour
{
    /// <summary>Par unidad/tema aplanado: da orden global a anterior/siguiente.</summary>
    private struct Entry
    {
        public TheoryUnitSO unit;
        public TheoryTopicSO topic;
        public Button navButton;
    }

    [Header("Datos")]
    [Tooltip("Programa completo del módulo (unidades, temas y bibliografía).")]
    [SerializeField] private TheorySyllabusSO syllabus;

    [Header("Navegación")]
    [SerializeField] private string backScene = "ModeSelection";

    [Header("Audio")]
    [SerializeField] private AudioClip uiSound;

    /// <summary>Clave de PlayerPrefs donde se guardan los temas ya leídos.</summary>
    private const string ReadPrefsKey = "TheoryTopicsRead";

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly HashSet<string> _read = new HashSet<string>();
    private readonly List<VisualElement> _unitGroups = new List<VisualElement>();

    private VisualElement _homeView;
    private VisualElement _readerView;
    private VisualElement _refsView;
    private VisualElement _footer;
    private VisualElement _content;
    private VisualElement _figure;
    private VisualElement _figureImage;
    private VisualElement _progressFill;
    private Label _figureCaption;
    private Label _crumb;
    private Label _title;
    private Label _summary;
    private Label _progress;
    private Label _searchHint;
    private ScrollView _bodyScroll;
    private ScrollView _nav;
    private TextField _search;
    private Button _prevButton;
    private Button _nextButton;

    private int _currentIndex = -1;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        UiSound.Configure(uiSound, uiSound, 1f, 1f);

        _homeView      = root.Q<VisualElement>("home-view");
        _readerView    = root.Q<VisualElement>("reader-view");
        _refsView      = root.Q<VisualElement>("refs-view");
        _footer        = root.Q<VisualElement>("reader-footer");
        _content       = root.Q<VisualElement>("content");
        _figure        = root.Q<VisualElement>("figure");
        _figureImage   = root.Q<VisualElement>("figure-image");
        _figureCaption = root.Q<Label>("figure-caption");
        _progressFill  = root.Q<VisualElement>("progress-fill");
        _crumb         = root.Q<Label>("crumb");
        _title         = root.Q<Label>("topic-title");
        _summary       = root.Q<Label>("topic-summary");
        _progress      = root.Q<Label>("progress");
        _searchHint    = root.Q<Label>("search-hint");
        _bodyScroll    = root.Q<ScrollView>("body-scroll");
        _nav           = root.Q<ScrollView>("nav");
        _search        = root.Q<TextField>("search");
        _prevButton    = root.Q<Button>("btn-prev");
        _nextButton    = root.Q<Button>("btn-next");

        LoadReadProgress();
        BuildFlatIndex();
        BuildHeader(root);
        BuildSidebar();
        BuildHomeCards();
        BuildReferences(root.Q<VisualElement>("refs-list"));

        root.Q<Button>("btn-exit").clicked += ExitModule;
        root.Q<Button>("btn-home").clicked += ShowHome;
        root.Q<Button>("btn-refs").clicked += ShowReferences;
        _prevButton.clicked += () => OpenTopic(_currentIndex - 1);
        _nextButton.clicked += () => OpenTopic(_currentIndex + 1);

        _search.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        UiSound.AttachAll(root);
        UpdateProgressBar();
        ShowHome();
    }

    // =====================================================================
    // Construcción
    // =====================================================================

    /// <summary>Aplana unidades y temas en una sola secuencia navegable.</summary>
    private void BuildFlatIndex()
    {
        _entries.Clear();
        if (syllabus == null || syllabus.units == null) return;

        foreach (TheoryUnitSO unit in syllabus.units)
        {
            if (unit == null || unit.topics == null) continue;
            foreach (TheoryTopicSO topic in unit.topics)
            {
                if (topic == null) continue;
                _entries.Add(new Entry { unit = unit, topic = topic });
            }
        }
    }

    private void BuildHeader(VisualElement root)
    {
        var subtitle = root.Q<Label>("header-subtitle");
        var homeCourse = root.Q<Label>("home-course");
        var homeMeta = root.Q<Label>("home-meta");
        if (syllabus == null) return;

        if (subtitle != null) subtitle.text = syllabus.courseName;
        if (homeCourse != null) homeCourse.text = syllabus.courseName;
        if (homeMeta != null)
            homeMeta.text = string.Join("  ·  ", new[]
            {
                syllabus.courseCode, syllabus.courseTrajectory, syllabus.courseCredits
            });
    }

    /// <summary>
    /// Índice lateral de dos niveles. Cada unidad es un botón que pliega o
    /// despliega su grupo de temas; los botones de tema quedan guardados en
    /// <see cref="Entry.navButton"/> para poder resaltarlos después.
    /// </summary>
    private void BuildSidebar()
    {
        if (_nav == null || syllabus == null || syllabus.units == null) return;

        _nav.Clear();
        _unitGroups.Clear();
        int flatIndex = 0;

        foreach (TheoryUnitSO unit in syllabus.units)
        {
            if (unit == null) continue;

            var group = new VisualElement();
            var topicsBox = new VisualElement();

            var unitButton = new Button { text = "UNIDAD " + unit.number + "  ·  " + unit.title };
            unitButton.AddToClassList("theory-unit");
            unitButton.clicked += () =>
            {
                bool open = topicsBox.style.display != DisplayStyle.None;
                topicsBox.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
                unitButton.EnableInClassList("theory-unit--open", !open);
            };

            group.Add(unitButton);
            group.Add(topicsBox);

            if (unit.topics != null)
            {
                foreach (TheoryTopicSO topic in unit.topics)
                {
                    if (topic == null) continue;

                    int index = flatIndex;
                    var topicButton = new Button { text = topic.title };
                    topicButton.AddToClassList("theory-topic");
                    topicButton.EnableInClassList("theory-topic--read", IsRead(topic));
                    topicButton.clicked += () => OpenTopic(index);

                    topicsBox.Add(topicButton);

                    Entry entry = _entries[flatIndex];
                    entry.navButton = topicButton;
                    _entries[flatIndex] = entry;
                    flatIndex++;
                }
            }

            // Arranca plegado: 11 unidades desplegadas de golpe serían otra
            // lista interminable, justo lo que se quería evitar.
            topicsBox.style.display = DisplayStyle.None;

            _nav.Add(group);
            _unitGroups.Add(group);
        }
    }

    /// <summary>Tarjetas de unidad de la portada.</summary>
    private void BuildHomeCards()
    {
        var container = _homeView?.Q<VisualElement>("home-units");
        if (container == null || syllabus == null || syllabus.units == null) return;

        container.Clear();
        int flatIndex = 0;

        foreach (TheoryUnitSO unit in syllabus.units)
        {
            if (unit == null) continue;

            int firstTopic = flatIndex;
            int count = unit.topics != null ? CountValid(unit.topics) : 0;
            flatIndex += count;

            var card = new Button();
            card.AddToClassList("theory-card");
            card.Add(MakeCardLabel("UNIDAD " + unit.number, "theory-card__num"));
            card.Add(MakeCardLabel(unit.title, "theory-card__title"));
            if (!string.IsNullOrWhiteSpace(unit.description))
                card.Add(MakeCardLabel(unit.description, "theory-card__desc"));
            card.Add(MakeCardLabel(count == 1 ? "1 tema" : count + " temas", "theory-card__count"));

            if (count > 0) card.clicked += () => OpenTopic(firstTopic);
            container.Add(card);
        }
    }

    private void BuildReferences(VisualElement list)
    {
        if (list == null) return;
        string refs = syllabus != null ? syllabus.references : null;
        if (string.IsNullOrWhiteSpace(refs))
        {
            TheoryMarkup.Render(list, "Aún no se han cargado las referencias del programa.");
            return;
        }

        // Una referencia por línea; se muestran como lista con viñeta.
        var markup = new System.Text.StringBuilder();
        foreach (string line in refs.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0) markup.Append("- ").Append(trimmed).Append('\n');
        }
        TheoryMarkup.Render(list, markup.ToString());
    }

    private static Label MakeCardLabel(string text, string ussClass)
    {
        var label = new Label(text);
        label.AddToClassList(ussClass);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static int CountValid(TheoryTopicSO[] topics)
    {
        int n = 0;
        foreach (TheoryTopicSO t in topics) if (t != null) n++;
        return n;
    }

    // =====================================================================
    // Cambio de estado
    // =====================================================================

    private void ShowState(VisualElement visible, bool showFooter)
    {
        _homeView.style.display   = visible == _homeView   ? DisplayStyle.Flex : DisplayStyle.None;
        _readerView.style.display = visible == _readerView ? DisplayStyle.Flex : DisplayStyle.None;
        _refsView.style.display   = visible == _refsView   ? DisplayStyle.Flex : DisplayStyle.None;
        _footer.style.display     = showFooter ? DisplayStyle.Flex : DisplayStyle.None;
        _bodyScroll.scrollOffset = Vector2.zero;
    }

    private void ShowHome()
    {
        _currentIndex = -1;
        HighlightCurrent();
        ShowState(_homeView, false);
    }

    private void ShowReferences()
    {
        _currentIndex = -1;
        HighlightCurrent();
        ShowState(_refsView, false);
    }

    private void OpenTopic(int index)
    {
        if (index < 0 || index >= _entries.Count) return;

        _currentIndex = index;
        Entry entry = _entries[index];
        TheoryTopicSO topic = entry.topic;

        _crumb.text = "UNIDAD " + entry.unit.number + "  ›  " + entry.unit.title;
        _title.text = topic.title;

        bool hasSummary = !string.IsNullOrWhiteSpace(topic.summary);
        _summary.style.display = hasSummary ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasSummary) _summary.text = TheoryMarkup.Inline(topic.summary);

        TheoryMarkup.Render(_content, topic.contentText);

        // Imagen opcional (igual que el módulo original: si no hay, no se dibuja
        // el hueco gris).
        if (topic.topicImage != null)
        {
            _figure.style.display = DisplayStyle.Flex;
            _figureImage.style.backgroundImage = new StyleBackground(topic.topicImage);
            bool hasCaption = !string.IsNullOrWhiteSpace(topic.imageCaption);
            _figureCaption.style.display = hasCaption ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasCaption) _figureCaption.text = topic.imageCaption;
        }
        else
        {
            _figure.style.display = DisplayStyle.None;
            _figureImage.style.backgroundImage = StyleKeyword.None;
        }

        _prevButton.SetEnabled(index > 0);
        _nextButton.SetEnabled(index < _entries.Count - 1);
        _progress.text = "Tema " + (index + 1) + " de " + _entries.Count;

        MarkAsRead(topic);
        HighlightCurrent();
        ExpandUnitOf(index);
        ShowState(_readerView, true);
    }

    /// <summary>Resalta en el índice el tema abierto y apaga el anterior.</summary>
    private void HighlightCurrent()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Button button = _entries[i].navButton;
            if (button == null) continue;
            button.EnableInClassList("theory-topic--current", i == _currentIndex);
            button.EnableInClassList("theory-topic--read", i != _currentIndex && IsRead(_entries[i].topic));
        }
    }

    /// <summary>Despliega la unidad del tema abierto y la trae a la vista.</summary>
    private void ExpandUnitOf(int index)
    {
        Button button = _entries[index].navButton;
        if (button == null) return;

        VisualElement topicsBox = button.parent;
        if (topicsBox != null) topicsBox.style.display = DisplayStyle.Flex;

        VisualElement group = topicsBox?.parent;
        var unitButton = group?.Q<Button>(className: "theory-unit");
        unitButton?.AddToClassList("theory-unit--open");

        // El layout del índice aún no está resuelto cuando se abre el primer
        // tema, así que el scroll se difiere un frame.
        _nav.schedule.Execute(() => _nav.ScrollTo(button)).ExecuteLater(0);
    }

    // =====================================================================
    // Búsqueda
    // =====================================================================

    /// <summary>
    /// Filtra el índice por título, resumen y contenido. Con filtro activo las
    /// unidades se despliegan solas y las que no tienen coincidencias se ocultan.
    /// </summary>
    private void ApplyFilter(string query)
    {
        if (_searchHint != null)
            _searchHint.style.display = string.IsNullOrEmpty(query) ? DisplayStyle.Flex : DisplayStyle.None;

        string needle = query == null ? string.Empty : query.Trim().ToLowerInvariant();
        bool filtering = needle.Length > 0;

        var matchesPerGroup = new Dictionary<VisualElement, int>();

        foreach (Entry entry in _entries)
        {
            if (entry.navButton == null) continue;

            bool match = !filtering || Matches(entry.topic, entry.unit, needle);
            entry.navButton.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;

            VisualElement group = entry.navButton.parent?.parent;
            if (group == null) continue;
            if (!matchesPerGroup.ContainsKey(group)) matchesPerGroup[group] = 0;
            if (match) matchesPerGroup[group]++;
        }

        foreach (VisualElement group in _unitGroups)
        {
            int matches = matchesPerGroup.TryGetValue(group, out int n) ? n : 0;
            group.style.display = (!filtering || matches > 0) ? DisplayStyle.Flex : DisplayStyle.None;

            // Con búsqueda activa se abren todas las unidades visibles: obligar
            // a desplegarlas a mano anularía la utilidad del filtro.
            if (group.childCount < 2) continue;
            group[1].style.display = filtering ? DisplayStyle.Flex : group[1].style.display;
            group.Q<Button>(className: "theory-unit")?.EnableInClassList("theory-unit--open", filtering);
        }
    }

    private static bool Matches(TheoryTopicSO topic, TheoryUnitSO unit, string needle)
    {
        return Contains(topic.title, needle)
            || Contains(topic.summary, needle)
            || Contains(topic.contentText, needle)
            || Contains(unit.title, needle);
    }

    private static bool Contains(string haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack) && haystack.ToLowerInvariant().Contains(needle);
    }

    // =====================================================================
    // Progreso de lectura (persistente)
    // =====================================================================

    private bool IsRead(TheoryTopicSO topic)
    {
        return topic != null && _read.Contains(topic.name);
    }

    private void MarkAsRead(TheoryTopicSO topic)
    {
        if (topic == null || !_read.Add(topic.name)) return;
        PlayerPrefs.SetString(ReadPrefsKey, string.Join("|", _read));
        PlayerPrefs.Save();
        UpdateProgressBar();
    }

    private void LoadReadProgress()
    {
        _read.Clear();
        string stored = PlayerPrefs.GetString(ReadPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(stored)) return;
        foreach (string name in stored.Split('|'))
            if (name.Length > 0) _read.Add(name);
    }

    private void UpdateProgressBar()
    {
        if (_progressFill == null || _entries.Count == 0) return;

        int read = 0;
        foreach (Entry entry in _entries) if (IsRead(entry.topic)) read++;
        _progressFill.style.width = Length.Percent(100f * read / _entries.Count);
    }

    // =====================================================================
    // Salida y teclado
    // =====================================================================

    private void ExitModule()
    {
        SceneManager.LoadScene(backScene);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        // Con el cursor en el buscador las flechas mueven el cursor de texto,
        // no el tema: no las interceptamos.
        var focused = _search?.panel?.focusController?.focusedElement as VisualElement;
        bool searchHasFocus = focused != null && (focused == _search || _search.Contains(focused));

        if (searchHasFocus)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                _search.value = string.Empty;
                _search.Blur();
                evt.StopPropagation();
            }
            return;
        }

        bool reading = _currentIndex >= 0;

        switch (evt.keyCode)
        {
            case KeyCode.LeftArrow:
                if (reading) { OpenTopic(_currentIndex - 1); evt.StopPropagation(); }
                break;
            case KeyCode.RightArrow:
                if (reading) { OpenTopic(_currentIndex + 1); evt.StopPropagation(); }
                break;
            case KeyCode.Escape:
                if (reading) ShowHome(); else ExitModule();
                evt.StopPropagation();
                break;
        }
    }
}
