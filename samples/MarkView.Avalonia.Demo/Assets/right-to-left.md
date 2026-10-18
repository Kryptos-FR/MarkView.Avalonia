# טקסט מימין לשמאל

כל פסקה, כותרת, רשימה, ציטוט וטבלה מקבלים את הכיוון של הטקסט שלהם, לפי האות הראשונה שיש לה כיוון, כמו `dir="auto"` ב-HTML. קוד נשאר תמיד משמאל לימין: הפקודה `console.log()` נשארת שלמה גם באמצע משפט בעברית.

This paragraph starts with an English word, so it reads left to right, even on this page.

## רשימות

- פריט ראשון עם **הדגשה** ו[קישור](https://github.com/Kryptos-FR/MarkView.Avalonia)
- פריט שני
  - פריט מקונן בעברית
  - Nested item in English
- 2026

1. התקינו את החבילה
2. הוסיפו את הרכיב לחלון
3. טענו קובץ Markdown

- [x] משימה שהושלמה
- [ ] משימה פתוחה

## ציטוט והתראה

> ציטוט בעברית: הקו נמצא בצד ימין.

> [!TIP]
> אפשר לכבות את הזיהוי עם `DetectTextDirection="False"`.

## טבלה

| שם | גרסה | תיאור |
|:---|---:|---|
| MarkView.Avalonia | 12.4 | הרכיב הראשי |
| MarkView.Avalonia.Math | 12.4 | נוסחאות |

## קוד

```csharp
// A code block is always left to right.
var viewer = new MarkdownViewer { Markdown = "# שלום" };
```

---

# النص من اليمين إلى اليسار

تأخذ كل فقرة اتجاه النص الخاص بها. هذه الجملة باللغة العربية، وتحتوي على شيفرة برمجية مثل `print("hello")` في المنتصف.

- العنصر الأول
- العنصر الثاني
- العنصر الثالث

> اقتباس باللغة العربية.

| الاسم | القيمة |
|---|---|
| واحد | ١ |
| اثنان | ٢ |

## The HTML `dir` attribute

<p dir="rtl">This English paragraph has dir="rtl", so it starts on the right.</p>

<p dir="ltr">לפסקה הזו יש dir="ltr", ולכן היא מתחילה משמאל.</p>
