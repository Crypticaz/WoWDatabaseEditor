using Antlr4.Runtime;
using WDE.Common.Database;
using WDE.PacketViewer.Filtering.Antlr;
using WDE.PacketViewer.ViewModels;

namespace WDE.PacketViewer.Filtering
{
    public class DatabaseExpressionEvaluator
    {
        private SyntaxLexer lexer;
        private CommonTokenStream tokens;
        private SyntaxParser parser;
        private ExpressionVisitor visitor;
        
        public DatabaseExpressionEvaluator(string expression)
        {
            lexer = new SyntaxLexer(new AntlrInputStream(expression));
            tokens = new CommonTokenStream(lexer);
            parser = new SyntaxParser(tokens);
            parser.BuildParseTree = true;
            parser.RemoveErrorListeners();

            visitor = new ExpressionVisitor();
        }

        public object? Evaluate(PacketViewModel entity)
        {
            lexer.Reset();
            tokens.Reset();
            parser.Reset();
            visitor.SetContext(entity);
            return visitor.Visit(parser.expr());
        }
    }
}