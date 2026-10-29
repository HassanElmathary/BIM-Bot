"""Entry point for the BIM-BOT application."""
import sys
import logging
from pathlib import Path

# Add src to python path to ensure absolute imports work
src_dir = Path(__file__).parent.absolute()
if str(src_dir) not in sys.path:
    sys.path.insert(0, str(src_dir))

def setup_logging():
    """Configure basic logging for the application."""
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        handlers=[
            logging.StreamHandler(),
            logging.FileHandler("bimbot_app.log")
        ]
    )

if __name__ == '__main__':
    setup_logging()
    logger = logging.getLogger("BIM-BOT")
    logger.info("Starting BIM-BOT Application...")
    
    try:
        from app import run
        run()
    except Exception as e:
        logger.error("Unhandled exception during startup", exc_info=True)
        sys.exit(1)
