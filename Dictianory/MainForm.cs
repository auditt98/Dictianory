using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

//using System.Net.NetworkInformation;

namespace Dictianory
{
    public partial class Main_Form : Form
    {
        //Dll import for global hotkey
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(String sClassName, String sAppName);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private IntPtr thisWindow;
        //vk Code for window keys
        private enum WindowKeys
        {
            Alt = 0x0001,
            Control = 0x0002,
            Shift = 0x0004, // Changes!
            Window = 0x0008,
            NoRepeat = 0x4000
        }

        //for offline search
        private DataTable dataTable;
        //correction data
        private Dictionary<string, int> correctionWords = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
        private BKTree bkTree = new BKTree();
        private bool populateCorrectionFinished = false;
        private ToolStripDropDown popup = new ToolStripDropDown();
        private Tuple<int, int> selectedWordIndexLength;
        //for expanding side menu
        private bool expand = true;
        private int activeSearchSource;
        private int activePanel;  
        //for keeping track of search sources
        private enum SearchSources
        { Database, Google, Dict }
        //for keeping track of panels
        private enum Panels
        { Search, Correction, Flashcard, Image, Paragraph, Learn };

        public Main_Form()
        {
            InitializeComponent();
        }

        private void Main_Form_Load(object sender, EventArgs e)
        {
            try
            {
                thisWindow = FindWindow(null, "Dictianory");
                RegisterHotKey(thisWindow, 0, (uint)WindowKeys.Control | (uint)WindowKeys.NoRepeat , (uint)Keys.Q);
                RegisterHotKey(thisWindow, 1, (uint)WindowKeys.Control | (uint)WindowKeys.NoRepeat , (uint)Keys.Y);
                init();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                throw;
            }
        }

        private void Main_Form_FormClosed(object sender, FormClosedEventArgs e)
        {
            UnregisterHotKey(thisWindow, 0);
            UnregisterHotKey(thisWindow, 1);
            dataTable.Dispose();
            popup.Dispose();
        }

        protected override void WndProc(ref Message keyPressed)
        {
            if (keyPressed.Msg == 0x0312)
            {
                if(keyPressed.WParam.ToInt32() == 0)
                {
                    SendKeys.SendWait("^{c}");
                    this.WindowState = FormWindowState.Maximized;
                    Search_Side_Button.PerformClick();
                    Search_TextBox.Text = "";
                    Search_TextBox.Focus();
                    SendKeys.SendWait("^{v}");
                }
                if(keyPressed.WParam.ToInt32() == 1)
                {
                    SendKeys.SendWait("^{c}");
                    this.WindowState = FormWindowState.Maximized;
                    Correction_Side_Button.PerformClick();
                    Correction_RichTextBox.Text = "";
                    Correction_RichTextBox.Focus();
                    SendKeys.SendWait("^{v}");
                }
            }
            base.WndProc(ref keyPressed);
        }

        private void init()
        {
            /*--------init---------- */
            BKTree_BW.RunWorkerAsync();
            uiInitiation();
            int databaseWordsCount = 1;
            //set search source
            activeSearchSource = (int)SearchSources.Database;
            uiInitiation();
            //set active search source to database
            Source_Database_Button.PerformClick(); 
            //Fill autocomplete source for search bar
            FillAutoComplete(ref databaseWordsCount);
            //Hide the speaker button      
            FillWOTD(ref databaseWordsCount);
            //for correction panel
            MyWords_Learning();          
        }

        private void uiInitiation()
        {
            StringHelper stringHelper = new StringHelper();
            //hide panels and color active panel
            activePanel = (int)Panels.Search;
            switchActivePanel();
            //default text for search result textbox
            Result_RichTextBox.AppendText("Hãy tìm kiếm một từ.");
            //hide pronounce button on search panel
            Pronounce_Button.Hide();
            //UI for correction panel
            Suggestions_ListBox.Hide();
            Correction_Learn_Popup_Button.Hide();
            Correction_Search_Popup_Button.Hide();
            popup.Margin = Padding.Empty;
            popup.Padding = Padding.Empty;
            Correction_RichTextBox.Text = stringHelper.getCorrectionPanelDefaultString();
            setToolTip();
        }

        private void setToolTip()
        {
            Source_Database_Tooltip.SetToolTip(Source_Database_Button, "Search using offline database");
            Source_Dictionary_Tooltip.SetToolTip(Source_Dictionary_Button, "Search using Dictionary.com");
            Source_Google_Tooltip.SetToolTip(Source_Google_Button, "Search using Google Translate");
            Pronounce_Button_Tooltip.SetToolTip(Pronounce_Button, "Listen");
            WOTD_Tooltip.SetToolTip(WOTD_Button, "Click to get the meaning of the word");
        }

        private void switchActivePanel()
        {
            switch (activePanel)
            {
                case (int)Panels.Search:
                    Learn_Main_Panel.Hide();
                    Paragraph_Main_Panel.Hide();
                    Correction_Main_Panel.Hide();
                    Image_Main_Panel.Hide();
                    Flashcard_Main_Panel.Hide();
                    Search_Side_Button.BackColor = Color.SteelBlue;
                    Correction_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Paragraph_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Flashcard_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Image_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Search_Main_Panel.Show();
                    Search_Main_Panel.Dock = DockStyle.Fill;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
                case (int)Panels.Correction:
                    Learn_Main_Panel.Hide();
                    Paragraph_Main_Panel.Hide();
                    Image_Main_Panel.Hide();
                    Flashcard_Main_Panel.Hide();
                    Search_Main_Panel.Hide();
                    Correction_Main_Panel.Show();
                    Correction_Main_Panel.Dock = DockStyle.Fill;
                    Correction_Side_Button.BackColor = Color.SteelBlue;
                    Search_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Paragraph_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Flashcard_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Image_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Side_Button.BackColor = Hamburger_Button.BackColor;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
                case (int)Panels.Flashcard:
                    Flashcard_Side_Button.BackColor = Color.SteelBlue;
                    Search_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Paragraph_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Correction_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Image_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Main_Panel.Hide();
                    Paragraph_Main_Panel.Hide();
                    Correction_Main_Panel.Hide();
                    Image_Main_Panel.Hide();
                    Search_Main_Panel.Hide();
                    Flashcard_Main_Panel.Show();
                    Flashcard_Main_Panel.Dock = DockStyle.Fill;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
                case (int)Panels.Image:
                    Image_Side_Button.BackColor = Color.SteelBlue;
                    Search_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Flashcard_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Correction_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Paragraph_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Main_Panel.Hide();
                    Paragraph_Main_Panel.Hide();
                    Flashcard_Main_Panel.Hide();
                    Search_Main_Panel.Hide();
                    Correction_Main_Panel.Hide();
                    Image_Main_Panel.Show();
                    Image_Main_Panel.Dock = DockStyle.Fill;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
                case (int)Panels.Learn:
                    Flashcard_Main_Panel.Hide();
                    Search_Main_Panel.Hide();
                    Image_Main_Panel.Hide();
                    Correction_Main_Panel.Hide();
                    Paragraph_Main_Panel.Hide();
                    Learn_Main_Panel.Show();
                    Learn_Main_Panel.Dock = DockStyle.Fill;
                    Learn_Side_Button.BackColor = Color.SteelBlue;
                    Search_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Flashcard_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Correction_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Paragraph_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Image_Side_Button.BackColor = Hamburger_Button.BackColor;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
                case (int)Panels.Paragraph:
                    Paragraph_Side_Button.BackColor = Color.SteelBlue;
                    Search_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Flashcard_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Correction_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Image_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Side_Button.BackColor = Hamburger_Button.BackColor;
                    Learn_Main_Panel.Hide();
                    Flashcard_Main_Panel.Hide();
                    Search_Main_Panel.Hide();
                    Image_Main_Panel.Hide();
                    Correction_Main_Panel.Hide();
                    Paragraph_Main_Panel.Show();
                    Paragraph_Main_Panel.Dock = DockStyle.Fill;
                    if (expand == true)
                    {
                        ToggleSlidingMenu();
                    }
                    break;
            }
        }

        #region Region Search

        private void FillAutoComplete(ref int wordsCount)
        {
            Search_TextBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            string query;
            SQLiteConnection sqliteConnection = new SQLiteConnection(ConnectionHelper.GetConnectionString());
            sqliteConnection.Open();

            dataTable = new DataTable();
            query = "Select Word,Ipa,Def from Words";

            SQLiteCommand sqliteCommand = new SQLiteCommand(query, sqliteConnection);
            SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(sqliteCommand);
            dataAdapter.Fill(dataTable);
            foreach (DataRow row in dataTable.Rows)
            {
                Search_TextBox.AutoCompleteCustomSource.Add(row["Word"].ToString());
                wordsCount++;
            }
            Search_TextBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
            dataAdapter.Dispose();
            sqliteConnection.Close();
            sqliteCommand.Dispose();
        }

        private void Search_TextBox_TextChanged(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (activeSearchSource == (int)SearchSources.Database)
            {
                DataRow[] found;
                string filter = "Word = '" + stringHelper.SQLChecker(Search_TextBox.Text) + "'";
                found = dataTable.Select(filter);
                if (found.Length > 0 && Search_TextBox.Text.Length > 0)
                {
                    Result_Word_Label.Text = found[0][0].ToString();
                    Result_RichTextBox.ResetText();
                    Result_RichTextBox.AppendText(found[0][1].ToString());
                    if (found[0][1].ToString().TrimEnd() != "" || found[0][1].ToString().TrimEnd() != "/")
                    {
                        Result_RichTextBox.AppendText(Environment.NewLine);
                    }
                    string def = found[0][2].ToString().Replace('=', '\t');
                    def = def.Replace('!', '\t');
                    def = def.Replace("+", ":");
                    Result_RichTextBox.AppendText(def);
                    colorResultRtb();

                    Pronounce_Button.Show();
                }
                else
                {
                    Result_RichTextBox.Text = stringHelper.errorWordNotFound();
                    Result_Word_Label.Text = "";
                    Pronounce_Button.Hide();
                }
            }
            else
            {
                //CALL YOUR CODE HERE
            }
        }

        private void colorResultRtb()
        {
            StringHelper stringHelper = new StringHelper();
            Regex r = new Regex(stringHelper.getRegexSearchResultWordType());
            foreach (Match match in r.Matches(Result_RichTextBox.Text))
            {
                Result_RichTextBox.Select(match.Index, match.Length);
                Result_RichTextBox.SelectionColor = Color.HotPink;
                Result_RichTextBox.SelectionFont = new Font(Result_RichTextBox.SelectionFont, FontStyle.Bold);
            }
            r = new Regex(stringHelper.getRegexSearchResultWordDef());
            foreach (Match match in r.Matches(Result_RichTextBox.Text))
            {
                Result_RichTextBox.Select(match.Index, match.Length);
                Result_RichTextBox.SelectionColor = Color.DarkTurquoise;
            }
            r = new Regex(stringHelper.getRegexSearchResultWordExample());
            foreach (Match match in r.Matches(Result_RichTextBox.Text))
            {
                Result_RichTextBox.Select(match.Index, match.Length);
                Result_RichTextBox.SelectionFont = new Font(Result_RichTextBox.SelectionFont, FontStyle.Italic);
            }
            r = null;
        }

        private void Pronounce_Button_Click(object sender, EventArgs e)
        {
            if (expand == true)
            {
                ToggleSlidingMenu();
            }
            SpeechSynthesizer speech = new SpeechSynthesizer();
            speech.SpeakAsync(Result_Word_Label.Text);
        }

        #endregion Region Search

        #region Region WordOfTheDay

        private int getRandomWordID(ref int databaseWordsCount)
        {
            Random random = new Random();
            int randomNumber = random.Next(1, databaseWordsCount);
            return randomNumber;
        }

        private void setWOTD(string wordOfTheDay)
        {
            WOTD_Button.Text = wordOfTheDay.ToUpper();
        }

        private void FillWOTD(ref int databaseWordsCount)
        {
            try
            {
                StringHelper stringHelper = new StringHelper();
                DateTime today = DateTime.Today;
                string sqlTimeFormat = stringHelper.getSqlTimeFormat(); //format of sql datetime datatype
                SQLiteConnection sqlConnection = new SQLiteConnection(ConnectionHelper.GetConnectionString());
                sqlConnection.Open();
                DataTable dateTable = new DataTable();
                string query = "Select * from LastDate";
                SQLiteCommand command = new SQLiteCommand(query, sqlConnection);
                SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(command);
                dataAdapter.Fill(dateTable);
                string wordOfTheDay;

                // if there's no record then create a new record for today

                if (dateTable.Rows.Count == 0)
                {
                    int randomWordID = getRandomWordID(ref databaseWordsCount);
                    string insert = @" insert into LastDate(lDate, wordId) values ('" + today.ToString(sqlTimeFormat) + "', " + randomWordID.ToString() + ")";
                    command = new SQLiteCommand(insert, sqlConnection);
                    command.ExecuteNonQuery();
                    wordOfTheDay = dataTable.Rows[randomWordID - 1]["Word"].ToString();
                    setWOTD(wordOfTheDay);
                }
                else // check if today is the same as the day stored in database
                {
                    DateTime sqlTime = DateTime.Parse(dateTable.Rows[0]["lDate"].ToString());
                    if (sqlTime.CompareTo(today) == 0) // if it is then just display the word stored
                    {
                        int wordId = int.Parse(dateTable.Rows[0]["wordId"].ToString());
                        wordOfTheDay = dataTable.Rows[wordId - 1]["Word"].ToString();
                        setWOTD(wordOfTheDay);
                    }
                    else // create a random word and update the database for today
                    {
                        int randomWordID = getRandomWordID(ref databaseWordsCount);
                        string update = @"update LastDate set lDate = '" + today.ToString(sqlTimeFormat) + "', wordId = " + randomWordID.ToString() + " where id = 1;";
                        command = new SQLiteCommand(update, sqlConnection);
                        command.ExecuteNonQuery();
                        wordOfTheDay = dataTable.Rows[randomWordID - 1]["Word"].ToString();
                        setWOTD(wordOfTheDay);
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
                throw;
            }
        }

        private void WOTD_Button_Click(object sender, EventArgs e)
        {
            Search_TextBox.Text = WOTD_Button.Text.ToLower();
        }

        #endregion Region WordOfTheDay

        #region Region Correction Panel

        private void populateSuggestions(string wordUnderMouse)
        {
            Dictionary<string, int> oneLevenshtein = new Dictionary<string, int>();
            Dictionary<string, int> twoLevenshtein = new Dictionary<string, int>();
            //search for all words having Levenshtein distance of one along with their weight
            foreach (var item in bkTree.Search(wordUnderMouse, 1))
            {
                oneLevenshtein.Add(item, correctionWords[item]);
            }
            //search for all words having Levenshtein distance of two along with their weight
            foreach (var item in bkTree.Search(wordUnderMouse, 2))
            {
                if (!oneLevenshtein.ContainsKey(item))
                {
                    twoLevenshtein.Add(item, correctionWords[item]);
                }
            }

            var oneList = oneLevenshtein.ToList();
            var twoList = twoLevenshtein.ToList();
            oneLevenshtein = null;
            twoLevenshtein = null;
            //sort the list by weight descending
            oneList.Sort((x, y) => (-1) * x.Value.CompareTo(y.Value));
            twoList.Sort((x, y) => (-1) * x.Value.CompareTo(y.Value));
            //only show 8 from each list
            if (oneList.Count > 8)
            {
                foreach (var item in oneList.GetRange(0, 7))
                {
                    Suggestions_ListBox.Items.Add(item.Key);
                }
            }
            else
            {
                foreach (var item in oneList)
                {
                    Suggestions_ListBox.Items.Add(item.Key);
                }
            }

            if (twoList.Count > 8)
            {
                foreach (var item in twoList.GetRange(0, 7))
                {
                    Suggestions_ListBox.Items.Add(item.Key);
                }
            }
            else
            {
                foreach (var item in twoList)
                {
                    Suggestions_ListBox.Items.Add(item.Key);
                }
            }
        }

        private void populateCorrectionData(string file)
        {
            var correctionData = File.ReadAllText(file, Encoding.UTF8);
            var wordPattern = new Regex(@"\b(\w*[a-zA-Z'-]\w*)\b");
            foreach (Match match in wordPattern.Matches(correctionData))
            {
                if (!correctionWords.ContainsKey(match.Value))
                {
                    correctionWords.Add(match.Value, 1);
                    bkTree.Add(match.Value);
                }
                else
                {
                    correctionWords[match.Value]++;
                }
            }
            correctionData = null;
            wordPattern = null;
        }

        private void BKTree_BW_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            populateCorrectionData("TrainingData.txt");
            populateCorrectionFinished = true;
            GC.Collect();
        }

        private void Correction_Check_Button_Click(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (populateCorrectionFinished == true)
            {
                if (Correction_RichTextBox.Text != stringHelper.getCorrectionPanelDefaultString())
                {
                    var wordPattern = new Regex(stringHelper.getRegexWordPatern());
                    foreach (Match match in wordPattern.Matches(Correction_RichTextBox.Text))
                    {
                        if (bkTree.Search(match.Value, 0).Count <= 0)
                        {
                            Correction_RichTextBox.Select(match.Index, match.Length);
                            Correction_RichTextBox.SelectionBackColor = Color.FromArgb(1, 243, 126, 120);
                        }
                        else
                        {
                            Correction_RichTextBox.Select(match.Index, match.Length);
                            Correction_RichTextBox.SelectionBackColor = Color.FromArgb(1, 59, 64, 69);
                        }
                    }
                    wordPattern = null;
                }
            }
            else
            {
                MessageBox.Show(stringHelper.infoPleaseWait());
            }
            stringHelper = null;
        }

        private void Correction_RichTextBox_TextChanged(object sender, EventArgs e)
        {
            Correction_RichTextBox.DeselectAll();
            if (Correction_RichTextBox.Text == "")
            {
                Correction_RichTextBox.SelectionBackColor = Color.FromArgb(1, 59, 64, 69);
                Correction_RichTextBox.ResetText();
            }
        }

        private void Correction_RichTextBox_Enter(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (Correction_RichTextBox.Text == stringHelper.getCorrectionPanelDefaultString())
            {
                Correction_RichTextBox.Text = "";
            }
            stringHelper = null;
        }

        private void Correction_RichTextBox_Leave(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (Correction_RichTextBox.Text == "")
            {
                Correction_RichTextBox.Text = stringHelper.getCorrectionPanelDefaultString();
            }
            stringHelper = null;
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Correction_RichTextBox.Cut();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Correction_RichTextBox.Copy();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Correction_RichTextBox.Paste();
        }

        private void checkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Correction_Check_Button.PerformClick();
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Correction_RichTextBox.SelectAll();
        }

        private void Correction_RichTextBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            //reset popup and listbox to prevent overflow
            resetPopup();
            //get the word under the mouse
            if (populateCorrectionFinished == true)
            {
                if (Correction_RichTextBox.Text != "")
                {
                    Point p = new Point(e.X, e.Y);
                    string wordUnderMouse = getWordUnderMouse(p);
                    bool isCorrect = true;

                    if (wordUnderMouse.Trim() != "" && bkTree.Search(wordUnderMouse, 0).Count == 0)
                    {
                        //show a popup
                        isCorrect = false;
                        populateSuggestions(wordUnderMouse);
                    }
                    showPopup(p, isCorrect);
                }
            }
            else
            {
                MessageBox.Show(stringHelper.infoPleaseWait());
            }
        }

        private string getWordUnderMouse(Point e)
        {
            int pos = Correction_RichTextBox.GetCharIndexFromPosition(new Point(e.X, e.Y));
            string txt = Correction_RichTextBox.Text;
            int startPos;
            for (startPos = pos; startPos >= 0; startPos--)
            {
                char ch = txt[startPos];
                if (!(char.IsLetter(ch) || ch == '-' || ch == '\''))
                {
                    break;
                }
            }
            startPos++;
            int endPos;
            for (endPos = pos; endPos < txt.Length; endPos++)
            {
                char ch = txt[endPos];
                if (!(char.IsLetter(ch) || ch == '-' || ch == '\''))
                {
                    break;
                }
            }
            endPos--;
            if (startPos > endPos)
            {
                return "";
            }
            else
            {
                Correction_RichTextBox.Select(startPos, endPos - startPos + 1);
                selectedWordIndexLength = new Tuple<int, int>(startPos, endPos - startPos + 1);
                return txt.Substring(startPos, endPos - startPos + 1);
            }
        }

        private void showPopup(Point p, bool isCorrect)
        {
            if (isCorrect == true)
            {
                ToolStripControlHost searchWord = new ToolStripControlHost(Correction_Search_Popup_Button);
                searchWord.Margin = Padding.Empty;
                searchWord.Padding = Padding.Empty;
                searchWord.Dock = DockStyle.Top;
                popup.Items.Add(searchWord);
            }
            else
            {
                ToolStripControlHost suggestionsList = new ToolStripControlHost(Suggestions_ListBox);
                ToolStripControlHost learnWord = new ToolStripControlHost(Correction_Learn_Popup_Button);
                suggestionsList.Padding = Padding.Empty;
                suggestionsList.Margin = Padding.Empty;
                learnWord.Padding = Padding.Empty;
                learnWord.Margin = Padding.Empty;
                suggestionsList.Dock = DockStyle.Top;
                learnWord.Dock = DockStyle.Top;
                if (Suggestions_ListBox.Items.Count != 0)
                {
                    popup.Items.Add(suggestionsList);
                }
                else
                {
                    Suggestions_ListBox.Hide();
                }
                popup.Items.Add(learnWord);
            }
            popup.Show(Correction_RichTextBox, p.X + 20, p.Y + 20);
        }

        private void resetPopup()
        {
            popup.Items.Clear();
            if (Suggestions_ListBox.Items.Count != 0)
            {
                Suggestions_ListBox.Items.Clear();
            }
            popup.Hide();
        }

        private void Suggestions_ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Correction_RichTextBox.Select(selectedWordIndexLength.Item1, selectedWordIndexLength.Item2);
            Correction_RichTextBox.SelectedText = Suggestions_ListBox.SelectedItem.ToString();
            Correction_Check_Button.PerformClick();
            resetPopup();
        }

        private void Correction_Learn_Button_Click(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (Correction_RichTextBox.Text != stringHelper.getCorrectionPanelDefaultString())
            {
                Correction_RichTextBox.Select(selectedWordIndexLength.Item1, selectedWordIndexLength.Item2);
                string word = Correction_RichTextBox.SelectedText;
                if (bkTree.Search(word, 0).Count == 0)
                {
                    File.AppendAllText(stringHelper.getDataTrainingFileName(), Environment.NewLine + word);
                    correctionWords.Add(word, 1);
                    bkTree.Add(word);
                    Correction_Check_Button.PerformClick();
                }
            }
        }

        private void Correction_RichTextBox_VScroll(object sender, EventArgs e)
        {
            resetPopup();
        }

        private void Correction_Learn_Popup_Button_Click(object sender, EventArgs e)
        {
            Correction_Learn_Button.PerformClick();
            resetPopup();
        }

        private void Correction_Search_Popup_Button_Click(object sender, EventArgs e)
        {
            StringHelper stringHelper = new StringHelper();
            if (Correction_RichTextBox.Text != stringHelper.getCorrectionPanelDefaultString())
            {
                Correction_RichTextBox.Select(selectedWordIndexLength.Item1, selectedWordIndexLength.Item2);
                string word = Correction_RichTextBox.SelectedText;
                resetPopup();
                Search_Side_Button.PerformClick();
                Search_TextBox.Text = word;
            }
        }

        #endregion Region Correction Panel

        #region Region SideButtonsClick Events

        private void Hamburger_Button_Click(object sender, EventArgs e)
        {
            ToggleSlidingMenu();
        }

        private void Search_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Search;
            switchActivePanel();
        }

        private void Correction_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Correction;
            switchActivePanel();
        }

        private void Flashcard_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Flashcard;
            switchActivePanel();
        }

        private void Image_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Image;
            switchActivePanel();
        }

        private void Paragraph_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Paragraph;
            switchActivePanel();
        }

        private void Learn_Side_Button_Click(object sender, EventArgs e)
        {
            activePanel = (int)Panels.Learn;
            switchActivePanel();
        }

        #endregion Region SideButtonsClick Events

        #region Region SideLabelClick Events

        private void Menu_Side_Label_Click(object sender, EventArgs e)
        {
            Hamburger_Button.PerformClick();
        }

        private void Search_Side_Label_Click(object sender, EventArgs e)
        {
            Search_Side_Button.PerformClick();
        }

        private void Correction_Side_Label_Click(object sender, EventArgs e)
        {
            Correction_Side_Button.PerformClick();
        }

        private void Flashcard_Side_Label_Click(object sender, EventArgs e)
        {
            Flashcard_Side_Button.PerformClick();
        }

        private void Image_Side_Label_Click(object sender, EventArgs e)
        {
            Image_Side_Button.PerformClick();
        }

        private void Paragraph_Side_Label_Click(object sender, EventArgs e)
        {
            Paragraph_Side_Button.PerformClick();
        }

        private void Learn_Label_Click(object sender, EventArgs e)
        {
            Learn_Side_Button.PerformClick();
        }

        #endregion Region SideLabelClick Events
   
        private void ToggleSlidingMenu()
        {
            if (expand == true)
            {
                Main_Panel.ColumnStyles[1].Width = 0;
                Main_Panel.ColumnStyles[2].Width = 95;
                expand = false;
            }
            else
            {
                Main_Panel.ColumnStyles[1].Width = 15;
                Main_Panel.ColumnStyles[2].Width = 80;
                expand = true;
            }
        }

        #region Region SearchSourceButton

        private void Source_Dictionary_Button_Click(object sender, EventArgs e)
        {
            activeSearchSource = (int)SearchSources.Dict;
            Source_Database_Button.BackColor = Color.White;
            Source_Google_Button.BackColor = Color.White;
            Source_Dictionary_Button.BackColor = Color.LightBlue;

            Result_RichTextBox.Text = "Hãy tìm kiếm một từ.";
            Result_Word_Label.Text = "";
            Pronounce_Button.Hide();
            
        }

        private void Source_Google_Button_Click(object sender, EventArgs e)
        {
            activeSearchSource = (int)SearchSources.Google;
            Source_Dictionary_Button.BackColor = Color.White;
            Source_Database_Button.BackColor = Color.White;
            Source_Google_Button.BackColor = Color.LightBlue;

            Result_RichTextBox.Text = "Hãy tìm kiếm một từ.";
            Result_Word_Label.Text = "";
            Pronounce_Button.Hide();
            
        }

        private void Source_Database_Button_Click(object sender, EventArgs e)
        {
            activeSearchSource = (int)SearchSources.Database;
            Source_Database_Button.BackColor = Color.LightBlue;
            Source_Dictionary_Button.BackColor = Color.White;
            Source_Google_Button.BackColor = Color.White;
        }

        #endregion Region SearchSourceButton

        #region Learning Panel

        /// Redraw TabControl
        private void LearnTabControl_DrawItem(Object sender, System.Windows.Forms.DrawItemEventArgs e)
        {
            Graphics g = e.Graphics;
            Brush _textBrush, _fillBrush;
            _fillBrush = new SolidBrush(Color.FromArgb(24, 24, 25));
            Pen p = new Pen(Color.FromArgb(24, 24, 25));
            // Get the item from the collection.
            TabPage _tabPage = Learn_TabC.TabPages[e.Index];

            // Get the real bounds for the tab rectangle.
            Rectangle _tabBounds = Learn_TabC.GetTabRect(e.Index);
            e.DrawBackground();
            if (e.State == DrawItemState.Selected)
            {
                // Draw a different background color, and don't paint a focus rectangle.
                _textBrush = new SolidBrush(Color.Gold);
                g.FillRectangle(_fillBrush, e.Bounds);
            }
            else
            {
                _textBrush = new System.Drawing.SolidBrush(Color.FromArgb(24, 24, 25));
            }

            // Use our own font.
            Font _tabFont = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Pixel);

            // Draw string. Center the text.
            StringFormat _stringFlags = new StringFormat();
            _stringFlags.Alignment = StringAlignment.Center;
            _stringFlags.LineAlignment = StringAlignment.Center;
            g.DrawString(_tabPage.Text, _tabFont, _textBrush, _tabBounds, new StringFormat(_stringFlags));
        }

        private DataTable getDataMyWords()
        {
            string query;
            SQLiteConnection sqliteConnection = new SQLiteConnection(ConnectionHelper.GetConnectionString());
            sqliteConnection.Open();
            DataTable dataTable1 = new DataTable();
            dataTable1 = new DataTable();
            query = "Select Word from MyWords, Words where Words.id = MyWords.id";

            SQLiteCommand sqliteCommand = new SQLiteCommand(query, sqliteConnection);
            SQLiteDataAdapter dataAdapter = new SQLiteDataAdapter(sqliteCommand);
            dataAdapter.Fill(dataTable1);
            sqliteConnection.Close();
            sqliteCommand.Dispose();
            dataAdapter.Dispose();

            return dataTable1;
        }

        private void MyWords_Learning()
        {
            ListBox list = new ListBox();
            list.DataSource = getDataMyWords();
            list.DisplayMember = "Word";
            Learning_Tab1.Controls.Add(list);
            list.Dock = DockStyle.Fill;

            //System.Windows.Forms.ListBox Learning_List;
            //Learning_List = new System.Windows.Forms.ListBox();
            //Learning_Tab1.Controls.Add(Learning_List);
            //Learning_List.BorderStyle = System.Windows.Forms.BorderStyle.None;
            //Learning_List.Dock = System.Windows.Forms.DockStyle.Fill;
            //Learning_List.FormattingEnabled = true;
            //Learning_List.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            //Learning_List.ItemHeight = 100;
            //Learning_List.Location = new System.Drawing.Point(3, 3);
            //Learning_List.Name = "Learning_List";
            //Learning_List.Size = new System.Drawing.Size(348, 410);
            //Learning_List.Font = new System.Drawing.Font("Segoe UI Semilight", 24, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            //Learning_List.DataSource = getDataMyWords();
            //Learning_List.DisplayMember = "Word";
        }

        #endregion Learning Panel
        
    }
}