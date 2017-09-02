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
            FunctionWordTextBox.Text = Contextualizer.Properties.Resources.function_word_list.ToString();

            foreach(var encoding in Encoding.GetEncodings())
            {
                EncodingDropdown.Items.Add(encoding.Name);
            }
            EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact(Encoding.Default.BodyName);


        }







        private void button1_Click(object sender, EventArgs e)
        {

            if (Convert.ToUInt32(WordWindowSizeTextbox.Text) < 1)
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
                saveFileDialog.ShowDialog();

                
                string OutputFileLocation = saveFileDialog.FileName;

                if (OutputFileLocation != "") { 
                    button1.Enabled = false;
                    WordWindowSizeTextbox.Enabled = false;
                    FunctionWordTextBox.Enabled = false;
                    ScanSubfolderCheckbox.Enabled = false;
                    PunctuationBox.Enabled = false;
                    EncodingDropdown.Enabled = false;
                    BgWorker.RunWorkerAsync(new string[] {TextFileFolder, OutputFileLocation});
                }
            } 

        }

        




        private void BgWorker_DoWork(object sender, DoWorkEventArgs e)
        {

            int WordWindowSize = 2;

            
            //set up our sentence boundary detection
            Regex NewlineClean = new Regex(@"[\r\n]+", RegexOptions.Compiled);

            //selects the text encoding based on user selection
            Encoding SelectedEncoding = null;
            this.Invoke((MethodInvoker)delegate ()
            {
                SelectedEncoding = Encoding.GetEncoding(EncodingDropdown.SelectedItem.ToString());
                WordWindowSize = Convert.ToInt32(WordWindowSizeTextbox.Text);
            });

            if (WordWindowSize < 1) WordWindowSize = 1;



            //the very first thing that we want to do is set up our function word lists
            List<string> WordWildcardList = new List<string>();
            List<string> WordsToHash = new List<string>();
            
            string[] OriginalFunctionWordList = NewlineClean.Split(FunctionWordTextBox.Text.ToLower());
            OriginalFunctionWordList = OriginalFunctionWordList.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            foreach(string Word in OriginalFunctionWordList)
            {
                string WordToParse = Word.Trim();

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



            try { 
            using (StreamWriter outputFile = new StreamWriter(((string[])e.Argument)[1]))
            {

                string HeaderString = "\"Filename\",\"Observation\",\"Word\",\"Context\"";

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
                    string readText = File.ReadAllText(fileName, SelectedEncoding).ToLower();


                    readText = NewlineClean.Replace(readText, " ");

                    //remove all the junk punctuation
                    foreach (char c in PunctuationBox.Text)
                    {
                        readText = readText.Replace(c, ' ');
                    }


                    uint NumberOfMatches = 0;

                    //splits everything out into words
                    string[] Words = readText.Trim().Split(' ');
                    Words = Words.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
                        for (int i = 0; i < Words.Length; i++)
                        {

                            bool IsWordMatched = false;

                            //this is where all of your magic is going to happen
                            if (HashedWords.Contains(Words[i]))
                            {
                                IsWordMatched = true;
                            }
                            else
                            {
                                for (int j = 0; j < WildCardWordListLength; j++)
                                {
                                    if (Words[i].StartsWith(WordsWithWildCards[j]))
                                    {
                                        IsWordMatched = true;
                                        break;
                                    }
                                }

                            }


                            if (IsWordMatched)
                            {
                                NumberOfMatches += 1;

                                //create a new array that will contain the word window
                                //string[] WordsInWindow = new string[1 + (WordWindowSize * 2)];
                                int SkipAmount = i - WordWindowSize;
                                int TakeAmount = (WordWindowSize * 2) + 1;

                                if (SkipAmount < 0)
                                {
                                    TakeAmount += SkipAmount;
                                    SkipAmount = 0;
                                }

                                if (i + WordWindowSize >= Words.Length)
                                {
                                    TakeAmount = Words.Length - SkipAmount;
                                }


                                
                                var WordsInWindow = Words.Skip(SkipAmount).Take(TakeAmount).ToArray();



                                //pull together the output
                                string[] OutputString = new string[] {"", "", "", ""};
                                OutputString[0] = "\"" + Filename_Clean + "\"";
                                OutputString[1] = NumberOfMatches.ToString();
                                OutputString[2] = "\"" + Words[i] + "\"";
                                OutputString[3] = "\"" + String.Join(" ", WordsInWindow) + "\"";

                                outputFile.WriteLine(String.Join(",", OutputString));
                            }
                            

                        }

                
       

                }


            }

            }
            catch
            {
                MessageBox.Show("Contextualizer could not open your output file\r\nfor writing. Is the file open in another application?");
            }



            
        }

        private void BgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button1.Enabled = true;
            WordWindowSizeTextbox.Enabled = true;
            FunctionWordTextBox.Enabled = true;
            ScanSubfolderCheckbox.Enabled = true;
            PunctuationBox.Enabled = true;
            EncodingDropdown.Enabled = true;
            FilenameLabel.Text = "Finished!";
            MessageBox.Show("Contextualizer has finished analyzing your texts.", "Analysis Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WordWindowSizeTextbox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }

        private void PhraseLengthTextbox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }

        private void BigWordTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }


    }
    


}
