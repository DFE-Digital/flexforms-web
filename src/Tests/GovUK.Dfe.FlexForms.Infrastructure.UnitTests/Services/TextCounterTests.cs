using GovUK.Dfe.FlexForms.Infrastructure.Services;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services
{
    public class TextCounterTests
    {
        [Fact]
        public void WordCount_of_empty_text_returns_zero()
        {
            int count = TextCounter.GetWordCount(string.Empty);
            Assert.Equal(0, count);
        }

        [Fact]
        public void WordCount_of_three_words_returns_3()
        {
            int count = TextCounter.GetWordCount("one two three");
            Assert.Equal(3, count);
        }

        [Fact]
        public void WordCount_of_three_words_with_tab_returns_3()
        {
            int count = TextCounter.GetWordCount("one\ttwo\tthree");
            Assert.Equal(3, count);
        }

        [Fact]
        public void WordCount_of_three_words_with_lf_returns_3()
        {
            int count = TextCounter.GetWordCount("one\ntwo\nthree");
            Assert.Equal(3, count);
        }

        [Fact]
        public void WordCount_of_three_words_with_crlf_returns_3()
        {
            int count = TextCounter.GetWordCount("one\r\ntwo\r\nthree");
            Assert.Equal(3, count);
        }
    }
}
