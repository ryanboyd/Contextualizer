using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;


namespace WindowsFormsApplication1
{

    public partial class Form1 : Form
    {


        //this is what runs at initialization
        public Form1()
        {

            InitializeComponent();
            WordListTextBox.Text = Contextualizer.Properties.Resources.function_word_list.ToString();

            foreach(var encoding in Encoding.GetEncodings())
            {
                EncodingDropdown.Items.Add(encoding.Name);
            }
            EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact(Encoding.Default.BodyName);


        }







        private void button1_Click(object sender, EventArgs e)
        {

            if (Convert.ToUInt32(WordWindowLeftTextbox.Text) < 1)
            {
                MessageBox.Show("Word Window Size must be >= 1.", "Problem with settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }



            FolderBrowser.Description = "Please choose the location of your .txt files";
            FolderBrowser.ShowDialog();
            string TextFileFolder = FolderBrowser.SelectedPath.ToString();

            if (TextFileFolder != "")
            {

                saveFileDialog.FileName = "Contextualizer.csv";

                saveFileDialog.InitialDirectory = TextFileFolder;
                if (saveFileDialog.ShowDialog() != DialogResult.Cancel) { 

                
                string OutputFileLocation = saveFileDialog.FileName;

                    if (OutputFileLocation != "") { 

                        while(WordListTextBox.Text.Contains("  "))
                        {
                            WordListTextBox.Text = WordListTextBox.Text.Replace("  ", " ");
                        }

                        button1.Enabled = false;
                        WordWindowLeftTextbox.Enabled = false;
                        WordWindowRightTextbox.Enabled = false;
                        WordListTextBox.Enabled = false;
                        ScanSubfolderCheckbox.Enabled = false;
                        PunctuationBox.Enabled = false;
                        EncodingDropdown.Enabled = false;
                        CaseSensitiveCheckbox.Enabled = false;
                        BgWorker.RunWorkerAsync(new string[] {TextFileFolder, OutputFileLocation});
                    }
                }
            } 

        }

        




        private void BgWorker_DoWork(object sender, DoWorkEventArgs e)
        {

            int WordWindowLeft = 2;
            int WordWindowRight = 2;
            bool CaseSensitive = false;

            
            //set up our sentence boundary detection
            Regex NewlineClean = new Regex(@"[\r\n]+", RegexOptions.Compiled);

            //selects the text encoding based on user selection
            Encoding SelectedEncoding = null;
            this.Invoke((MethodInvoker)delegate ()
            {
                SelectedEncoding = Encoding.GetEncoding(EncodingDropdown.SelectedItem.ToString());
                WordWindowLeft = Convert.ToInt32(WordWindowLeftTextbox.Text);
                WordWindowRight = Convert.ToInt32(WordWindowRightTextbox.Text);

                CaseSensitive = CaseSensitiveCheckbox.Checked;

            });

            //if (WordWindowSize < 1) WordWindowSize = 1;



            //the very first thing that we want to do is set up our function word lists
            List<string> WordWildcardList = new List<string>();
            List<string> WordsToHash = new List<string>();

            string[] OriginalWordList;

            if (CaseSensitive)
            {
                OriginalWordList = NewlineClean.Split(WordListTextBox.Text);
            }
            else
            {
                OriginalWordList = NewlineClean.Split(WordListTextBox.Text.ToLower());
            }

            OriginalWordList = OriginalWordList.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            int MaxWords = 1;

            foreach(string Word in OriginalWordList)
            {
                string WordToParse = Word.Trim();

                int numwords = WordToParse.Split().Length;

                if (numwords > MaxWords) MaxWords = numwords;

                if (WordToParse.Contains('*'))
                {
                    WordWildcardList.Add(WordToParse.Replace("*", ""));
                }
                else
                {
                    WordsToHash.Add(WordToParse);
                }

            }

            //remove duplicates
            WordWildcardList = WordWildcardList.Distinct().ToList();
            WordsToHash = WordsToHash.Distinct().ToList();

            HashSet<string> HashedWords = new HashSet<string>(WordsToHash);
            string[] WordsWithWildCards = WordWildcardList.ToArray();

            WordsToHash = null;
            WordWildcardList = null;

            int WildCardWordListLength = WordsWithWildCards.Length;


            //get the list of files
            var SearchDepth = SearchOption.TopDirectoryOnly;
            if (ScanSubfolderCheckbox.Checked)
            {
                SearchDepth = SearchOption.AllDirectories;
            }
            var files = Directory.EnumerateFiles( ((string[])e.Argument)[0], "*.txt", SearchDepth);



            //try { 
            using (StreamWriter outputFile = new StreamWriter(((string[])e.Argument)[1]))
            {

                string HeaderString = "\"Filename\",\"Observation\",\"DictionaryWord\",\"ContextLeft\",\"Match\",\"ContextRight\"";

                outputFile.WriteLine(HeaderString);


                foreach (string fileName in files)
                {

                    //set up our variables to report
                    string Filename_Clean = Path.GetFileName(fileName);
                    

                    //report what we're working on
                    FilenameLabel.Invoke((MethodInvoker)delegate
                    {
                        FilenameLabel.Text = "Analyzing: " + Filename_Clean;
                    });




                    //do stuff here
                    string readText;
                    if (CaseSensitive)
                    {
                        readText = File.ReadAllText(fileName, SelectedEncoding);
                    }
                    else
                    {
                        readText = File.ReadAllText(fileName, SelectedEncoding).ToLower();
                    }
                        


                    readText = NewlineClean.Replace(readText, " ");

                    //remove all the junk punctuation
                    foreach (char c in PunctuationBox.Text)
                    {
                        readText = readText.Replace(c, ' ');
                    }


                    uint NumberOfMatches = 0;

                    //splits everything out into words
                    string DictionaryEntry = "";
                    string[] Words = readText.Trim().Split(' ');

                    

                    Words = Words.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                    int TotalStringLength = Words.Length;

                    for (int i = 0; i < TotalStringLength; i++)
                        {


                            bool IsWordMatched = false;
                            int NumWordsInMatchedString = 0;
                            string WordToMatch = "";

                            for (int NumWords = MaxWords; NumWords > 0; NumWords--)
                            {

                                //here, we go in and construct n-grams up to the length of the 
                                //largest word phrase in the user-supplied list

                                if (i + NumWords > TotalStringLength) continue;
                                

                                string[] SubArray = new string[NumWords];
                                Array.Copy(Words, i, SubArray, 0, NumWords);

                              


                                WordToMatch = string.Join(" ", SubArray);


                                

                                //this is where all of your magic is going to happen
                                if (HashedWords.Contains(WordToMatch))
                                {
                                    IsWordMatched = true;
                                    DictionaryEntry = WordToMatch;
                                    NumWordsInMatchedString = NumWords;
                                }
                                else
                                {
                                    for (int j = 0; j < WildCardWordListLength; j++)
                                    {
                                        if (WordToMatch.StartsWith(WordsWithWildCards[j]))
                                        {
                                            IsWordMatched = true;
                                            DictionaryEntry = WordsWithWildCards[j] + "*";
                                            NumWordsInMatchedString = NumWords;
                                            break;
                                        }
                                    }

                                }

                                //if we found the word / phrase, we'll break out of this internal n-gram loop
                                if (IsWordMatched) break;


                            }


                            if (IsWordMatched)
                            {
                                NumberOfMatches += 1;

                                //create a new array that will contain the word window
                                //string[] WordsInWindow = new string[1 + (WordWindowSize * 2)];
                                int SkipPositionLeft = i - WordWindowLeft;
                                int SkipPositionRight = i + 1 + (NumWordsInMatchedString - 1);

                                int TakeLeft = WordWindowLeft;
                                int TakeRight = WordWindowRight;
                                

                            if (SkipPositionLeft < 0)
                                {

                                    TakeLeft = i;
                                    SkipPositionLeft = 0;
                                    
                                }

                                if (SkipPositionRight + TakeRight >= TotalStringLength)
                                {
                                    TakeRight = (SkipPositionRight + TakeRight) - TotalStringLength;
                                }


                                
                                var ContextLeft = Words.Skip(SkipPositionLeft).Take(TakeLeft).ToArray();
                                var ContextRight = Words.Skip(SkipPositionRight).Take(TakeRight).ToArray();



                            //pull together the output
                            string[] OutputString = new string[] {"", "", "", "", "", ""};
                                OutputString[0] = "\"" + Filename_Clean + "\"";
                                OutputString[1] = NumberOfMatches.ToString();
                                OutputString[2] = "\"" + DictionaryEntry + "\"";
                                OutputString[3] = "\"" + String.Join(" ", ContextLeft).Replace("\"", "\"\"") + "\"";
                                OutputString[4] = "\"" + WordToMatch + "\"";
                                OutputString[5] = "\"" + String.Join(" ", ContextRight).Replace("\"", "\"\"") + "\"";


                            outputFile.WriteLine(String.Join(",", OutputString));
                            }
                            

                        }

                
       

                }


            }

            //}
            //catch
            //{
            //    MessageBox.Show("Contextualizer could not open your output file\r\nfor writing. Is the file open in another application?");
           // }



            
        }

        private void BgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button1.Enabled = true;
            WordWindowLeftTextbox.Enabled = true;
            WordWindowRightTextbox.Enabled = true;
            WordListTextBox.Enabled = true;
            ScanSubfolderCheckbox.Enabled = true;
            PunctuationBox.Enabled = true;
            EncodingDropdown.Enabled = true;
            CaseSensitiveCheckbox.Enabled = true;
            FilenameLabel.Text = "Finished!";
            MessageBox.Show("Contextualizer has finished analyzing your texts.", "Analysis Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WordWindowSizeTextbox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }



    }
    


}
