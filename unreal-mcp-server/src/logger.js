/**
 * Structured Logger for BIM-Bot Unreal Server
 * 
 * Outputs JSON-structured log lines to stderr.
 * stdout is reserved for the MCP stdio transport — NEVER log there.
 * 
 * Configure via LOG_LEVEL env var: debug | info | warn | error (default: info)
 */

const LEVELS = { debug: 0, info: 1, warn: 2, error: 3 };

class Logger {
  constructor(name = 'bim-bot-unreal') {
    this.name = name;
    const envLevel = (process.env.LOG_LEVEL || 'info').toLowerCase();
    this.level = LEVELS[envLevel] ?? LEVELS.info;
  }

  _write(level, msg, data) {
    if (LEVELS[level] < this.level) return;

    const entry = {
      level,
      ts: new Date().toISOString(),
      logger: this.name,
      msg,
    };
    if (data !== undefined && data !== null) {
      entry.data = data;
    }

    process.stderr.write(JSON.stringify(entry) + '\n');
  }

  debug(msg, data) { this._write('debug', msg, data); }
  info(msg, data) { this._write('info', msg, data); }
  warn(msg, data) { this._write('warn', msg, data); }
  error(msg, data) { this._write('error', msg, data); }

  /** Create a child logger with a sub-name */
  child(name) {
    const child = new Logger(`${this.name}:${name}`);
    child.level = this.level;
    return child;
  }
}

const logger = new Logger();

module.exports = { Logger, logger };
